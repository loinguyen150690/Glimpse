using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Text;
using Glimpse.Core.Extensibility;
using Glimpse.Core.Extensions;
using Glimpse.Core.Message;
using Glimpse.Core.ResourceResult;
using Glimpse.Core.Tab.Assist;
using Tavis.UriTemplates;

#if NET35
using Glimpse.Core.Backport;
#endif

namespace Glimpse.Core.Framework
{
    /// <summary>
    /// The heart and soul of Glimpse. The runtime coordinate all input from a <see cref="IFrameworkProvider" />, persists collected runtime information and writes responses out to the <see cref="IFrameworkProvider" />.
    /// </summary>
    public class GlimpseRuntime : IGlimpseRuntime
    {
        private static readonly MethodInfo MethodInfoBeginRequest = typeof(GlimpseRuntime).GetMethod("BeginRequest", BindingFlags.Public | BindingFlags.Instance);
        private static readonly MethodInfo MethodInfoEndRequest = typeof(GlimpseRuntime).GetMethod("EndRequest", BindingFlags.Public | BindingFlags.Instance);
        private static readonly object LockObj = new object();
        private static GlimpseRuntime instance = null;

        /// <summary>
        /// Initializes static members of the <see cref="GlimpseRuntime" /> class.
        /// </summary>
        /// <exception cref="System.NullReferenceException">BeginRequest method not found</exception>
        static GlimpseRuntime()
        {
            // Version is in major.minor.build format to support http://semver.org/
            // TODO: Consider adding configuration hash to version
            Version = Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
            IsInitialized = false;

            if (MethodInfoBeginRequest == null)
            {
                throw new NullReferenceException("BeginRequest method not found");
            }

            if (MethodInfoEndRequest == null)
            {
                throw new NullReferenceException("EndRequest method not found");
            }
        }

        internal static void Reset()
        {
            instance = null; // HACK?
        }

        public static GlimpseRuntime Instance 
        {
            get
            {
                if (instance == null)
                {
                    throw new GlimpseException("Call GlimpseRuntime.Initialize before accessing the singleton Instance.");
                }

                return instance;
            }

            private set { instance = value; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GlimpseRuntime" /> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <exception cref="System.ArgumentNullException">Throws an exception if <paramref name="configuration"/> is <c>null</c>.</exception>
        public static void Initialize(IGlimpseConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException("configuration");
            }


            Instance = new GlimpseRuntime(configuration);
        }

        public GlimpseRuntime(IGlimpseConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException("configuration");
            }

            Configuration = configuration;
            this.Initialize();
        }

        /// <summary>
        /// Gets the executing version of Glimpse.
        /// </summary>
        /// <value>
        /// The version of Glimpse.
        /// </value>
        /// <remarks>Glimpse versioning follows the rules of <see href="http://semver.org/">Semantic Versioning</see>.</remarks>
        public static string Version { get; private set; }

        /// <summary>
        /// Gets or sets the configuration.
        /// </summary>
        /// <value>
        /// The configuration.
        /// </value>
        public IGlimpseConfiguration Configuration { get; set; }

        /// <summary>
        /// Gets a value indicating whether this instance has been initialized.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is initialized; otherwise, <c>false</c>.
        /// </value>
        public static bool IsInitialized { get; private set; }

        private IDictionary<string, TabResult> GetTabResultsStore(IFrameworkProvider frameworkProvider)
        {
            var requestStore = frameworkProvider.HttpRequestStore;
            var result = requestStore.Get<IDictionary<string, TabResult>>(Constants.TabResultsDataStoreKey);

            if (result == null)
            {
                result = new Dictionary<string, TabResult>();
                requestStore.Set(Constants.TabResultsDataStoreKey, result);
            }

            return result;
        }

        private IDictionary<string, TabResult> GetDisplayResultsStore(IFrameworkProvider frameworkProvider)
        {
            var requestStore = frameworkProvider.HttpRequestStore;
            var result = requestStore.Get<IDictionary<string, TabResult>>(Constants.DisplayResultsDataStoreKey);

            if (result == null)
            {
                result = new Dictionary<string, TabResult>();
                requestStore.Set(Constants.DisplayResultsDataStoreKey, result);
            }

            return result;
        }

        /// <summary>
        /// Begins Glimpse's processing of a Http request.
        /// </summary>
        /// <exception cref="Glimpse.Core.Framework.GlimpseException">Throws an exception if <see cref="GlimpseRuntime"/> is not yet initialized.</exception>
        public void BeginRequest(IFrameworkProvider frameworkProvider)
        {
            if (!IsInitialized)
            {
                throw new GlimpseException(Resources.BeginRequestOutOfOrderRuntimeMethodCall);
            }

            CallContext.LogicalSetData("fp", frameworkProvider);

            if (HasOffRuntimePolicy(RuntimeEvent.BeginRequest, frameworkProvider))
                return;

            ExecuteTabs(RuntimeEvent.BeginRequest, frameworkProvider);

            var requestStore = frameworkProvider.HttpRequestStore;

            // Give Request an ID
            var requestId = Guid.NewGuid();
            requestStore.Set(Constants.RequestIdKey, requestId);
            Func<Guid?, string> generateClientScripts = (rId) => rId.HasValue ? GenerateScriptTags(rId.Value, frameworkProvider) : GenerateScriptTags(requestId, frameworkProvider);
            requestStore.Set(Constants.ClientScriptsStrategy, generateClientScripts);
            
            var executionTimer = CreateAndStartGlobalExecutionTimer(requestStore);

            Configuration.MessageBroker.Publish(new RuntimeMessage().AsSourceMessage(typeof(GlimpseRuntime), MethodInfoBeginRequest).AsTimelineMessage("Start Request", TimelineCategory.Request).AsTimedMessage(executionTimer.Point()));
        }

        private bool HasOffRuntimePolicy(RuntimeEvent policyName, IFrameworkProvider frameworkProvider)
        {
            var policy = GetRuntimePolicy(policyName, frameworkProvider);
            if (policy.HasFlag(RuntimePolicy.Off))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Ends Glimpse's processing a Http request.
        /// </summary>
        /// <exception cref="Glimpse.Core.Framework.GlimpseException">Throws an exception if <c>BeginRequest</c> has not yet been called on a given request.</exception>
        public void EndRequest(IFrameworkProvider frameworkProvider) // TODO: Add PRG support
        {
            if (HasOffRuntimePolicy(RuntimeEvent.EndRequest, frameworkProvider))
                return;

            var requestStore = frameworkProvider.HttpRequestStore;

            var executionTimer = requestStore.Get<ExecutionTimer>(Constants.GlobalTimerKey);
            if (executionTimer != null)
            {
                Configuration.MessageBroker.Publish(new RuntimeMessage().AsSourceMessage(typeof(GlimpseRuntime), MethodInfoBeginRequest).AsTimelineMessage("End Request", TimelineCategory.Request).AsTimedMessage(executionTimer.Point()));
            }

            ExecuteTabs(RuntimeEvent.EndRequest, frameworkProvider);
            ExecuteDisplays(frameworkProvider);

            Guid requestId;
            Stopwatch stopwatch;
            try
            {
                requestId = requestStore.Get<Guid>(Constants.RequestIdKey);
                stopwatch = requestStore.Get<Stopwatch>(Constants.GlobalStopwatchKey);
                stopwatch.Stop();
            }
            catch (NullReferenceException ex)
            {
                throw new GlimpseException(Resources.EndRequestOutOfOrderRuntimeMethodCall, ex);
            }

            var requestMetadata = frameworkProvider.RequestMetadata;
            var policy = GetRuntimePolicy(RuntimeEvent.EndRequest, frameworkProvider);
            if (policy.HasFlag(RuntimePolicy.PersistResults))
            {
                var persistenceStore = Configuration.PersistenceStore;

                var metadata = new GlimpseRequest(requestId, requestMetadata, GetTabResultsStore(frameworkProvider), GetDisplayResultsStore(frameworkProvider), stopwatch.Elapsed);

                try
                {
                    persistenceStore.Save(metadata);
                }
                catch (Exception exception)
                {
                    Configuration.Logger.Error(Resources.GlimpseRuntimeEndRequesPersistError, exception, persistenceStore.GetType());
                }
            }

            if (policy.HasFlag(RuntimePolicy.ModifyResponseHeaders))
            {
                frameworkProvider.SetHttpResponseHeader(Constants.HttpResponseHeader, requestId.ToString());

                if (requestMetadata.GetCookie(Constants.ClientIdCookieName) == null)
                {
                    frameworkProvider.SetCookie(Constants.ClientIdCookieName, requestMetadata.ClientId);
                }
            }

            if (policy.HasFlag(RuntimePolicy.DisplayGlimpseClient))
            {
                var html = GenerateScriptTags(requestId, frameworkProvider);

                frameworkProvider.InjectHttpResponseBody(html);
            }
        }

        /// <summary>
        /// Executes the default resource.
        /// </summary>
        public void ExecuteDefaultResource(IFrameworkProvider frameworkProvider)
        {
            ExecuteResource(frameworkProvider, Configuration.DefaultResource.Name, ResourceParameters.None());
        }

        /// <summary>
        /// Begins access to session data.
        /// </summary>
        public void BeginSessionAccess(IFrameworkProvider frameworkProvider)
        {
            if (HasOffRuntimePolicy(RuntimeEvent.BeginSessionAccess, frameworkProvider))
                return;
           

            ExecuteTabs(RuntimeEvent.BeginSessionAccess, frameworkProvider);
        }

        /// <summary>
        /// Ends access to session data.
        /// </summary>
        public void EndSessionAccess(IFrameworkProvider frameworkProvider)
        {
            if (HasOffRuntimePolicy(RuntimeEvent.EndSessionAccess, frameworkProvider))
                return;

            ExecuteTabs(RuntimeEvent.EndSessionAccess, frameworkProvider);
        }

        /// <summary>
        /// Executes the resource.
        /// </summary>
        /// <param name="resourceName">Name of the resource.</param>
        /// <param name="parameters">The parameters.</param>
        /// <exception cref="System.ArgumentNullException">Throws an exception if either parameter is <c>null</c>.</exception>
        public void ExecuteResource(IFrameworkProvider frameworkProvider, string resourceName, ResourceParameters parameters)
        {
            if (string.IsNullOrEmpty(resourceName))
            {
                throw new ArgumentNullException("resourceName");
            }

            if (parameters == null)
            {
                throw new ArgumentNullException("parameters");
            }

            CallContext.LogicalSetData("fp", frameworkProvider);

            string message;
            var logger = Configuration.Logger;
            var context = new ResourceResultContext(logger, frameworkProvider, Configuration.Serializer, Configuration.HtmlEncoder);
            IResourceResult result;

            var policy = GetRuntimePolicy(RuntimeEvent.ExecuteResource, frameworkProvider);
            if (policy == RuntimePolicy.Off && !resourceName.Equals(Configuration.DefaultResource.Name))
            {
                message = string.Format(Resources.ExecuteResourceInsufficientPolicy, resourceName);
                logger.Info(message);
                new StatusCodeResourceResult(403, message).Execute(context);
                return;
            }

            var resources =
                Configuration.Resources.Where(
                    r => r.Name.Equals(resourceName, StringComparison.InvariantCultureIgnoreCase));

            switch (resources.Count())
            {
                case 1: // 200 - OK
                    try
                    {
                        var resource = resources.First();
                        var resourceContext = new ResourceContext(parameters.GetParametersFor(resource), Configuration.PersistenceStore, logger);

                        var privilegedResource = resource as IPrivilegedResource;

                        if (privilegedResource != null)
                        {
                            result = privilegedResource.Execute(resourceContext, Configuration, frameworkProvider);
                        }
                        else
                        {
                            result = resource.Execute(resourceContext);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Error(Resources.GlimpseRuntimeExecuteResourceError, ex, resourceName);
                        result = new ExceptionResourceResult(ex);
                    }

                    break;
                case 0: // 404 - File Not Found
                    message = string.Format(Resources.ExecuteResourceMissingError, resourceName);
                    logger.Warn(message);
                    result = new StatusCodeResourceResult(404, message);
                    break;
                default: // 500 - Server Error
                    message = string.Format(Resources.ExecuteResourceDuplicateError, resourceName);
                    logger.Warn(message);
                    result = new StatusCodeResourceResult(500, message);
                    break;
            }

            try
            {
                result.Execute(context);
            }
            catch (Exception exception)
            {
                logger.Fatal(Resources.GlimpseRuntimeExecuteResourceResultError, exception, result.GetType());
            }
        }

        /// <summary>
        /// Initializes this instance of the Glimpse runtime.
        /// </summary>
        /// <returns>
        ///   <c>true</c> if system initialized successfully, <c>false</c> otherwise
        /// </returns>
        private bool Initialize()
        {
            // TODO: This needs to be handled in BeginRequest
            // CreateAndStartGlobalExecutionTimer(Configuration.FrameworkProvider.HttpRequestStore);

            var logger = Configuration.Logger;
            // var policy = GetRuntimePolicy(RuntimeEvent.Initialize);
            var policy = Configuration.DefaultRuntimePolicy; // TODO: fix this up
            if (policy == RuntimePolicy.Off)
            {
                return false;
            }

            // Double checked lock to ensure thread safety. http://en.wikipedia.org/wiki/Double_checked_locking_pattern
            if (!IsInitialized)
            {
                lock (LockObj)
                {
                    if (!IsInitialized)
                    {
                        var messageBroker = Configuration.MessageBroker;

                        // TODO: Fix this to IDisplay no longer uses I*Tab*Setup
                        var displaysThatRequireSetup = Configuration.Displays.Where(display => display is ITabSetup).Select(display => display);
                        foreach (ITabSetup display in displaysThatRequireSetup)
                        {
                            var key = CreateKey(display);
                            try
                            {
                                var setupContext = new TabSetupContext(logger, messageBroker, () => GetTabStore(key, CallContext.LogicalGetData("fp") as IFrameworkProvider));
                                display.Setup(setupContext);
                            }
                            catch (Exception exception)
                            {
                                logger.Error(Resources.InitializeTabError, exception, key);
                            }
                        }


                        var tabsThatRequireSetup = Configuration.Tabs.Where(tab => tab is ITabSetup).Select(tab => tab);
                        foreach (ITabSetup tab in tabsThatRequireSetup)
                        {
                            var key = CreateKey(tab);
                            try
                            {
                                var setupContext = new TabSetupContext(logger, messageBroker, () => GetTabStore(key, CallContext.LogicalGetData("fp") as IFrameworkProvider));
                                tab.Setup(setupContext);
                            }
                            catch (Exception exception)
                            {
                                logger.Error(Resources.InitializeTabError, exception, key);
                            }
                        }

                        var inspectorContext = new InspectorContext(logger, Configuration.ProxyFactory, messageBroker, Configuration.TimerStrategy, Configuration.RuntimePolicyStrategy);

                        foreach (var inspector in Configuration.Inspectors)
                        {
                            try
                            {
                                inspector.Setup(inspectorContext);
                                logger.Debug(Resources.GlimpseRuntimeInitializeSetupInspector, inspector.GetType());
                            }
                            catch (Exception exception)
                            {
                                logger.Error(Resources.InitializeInspectorError, exception, inspector.GetType());
                            }
                        }

                        PersistMetadata();

                        IsInitialized = true;
                    }
                }
            }

            return policy != RuntimePolicy.Off;
        }

        private static UriTemplate SetParameters(UriTemplate template, IEnumerable<KeyValuePair<string, string>> nameValues)
        {
            if (nameValues == null)
            {
                return template;
            }

            foreach (var pair in nameValues)
            {
                template.SetParameter(pair.Key, pair.Value);
            }

            return template;
        }

        private static ExecutionTimer CreateAndStartGlobalExecutionTimer(IDataStore requestStore)
        {
            if (requestStore.Contains(Constants.GlobalStopwatchKey) && requestStore.Contains(Constants.GlobalTimerKey))
            {
                return requestStore.Get<ExecutionTimer>(Constants.GlobalTimerKey);
            }

            // Create and start global stopwatch
            var stopwatch = Stopwatch.StartNew();
            var executionTimer = new ExecutionTimer(stopwatch);
            requestStore.Set(Constants.GlobalStopwatchKey, stopwatch);
            requestStore.Set(Constants.GlobalTimerKey, executionTimer);
            return executionTimer;
        }

        private static string CreateKey(object obj)
        {
            string result;
            var keyProvider = obj as IKey;

            if (keyProvider != null)
            {
                result = keyProvider.Key;
            }
            else
            {
                result = obj.GetType().FullName;
            }

            return result
                .Replace('.', '_')
                .Replace(' ', '_')
                .ToLower();
        }

        private IDataStore GetTabStore(string tabName, IFrameworkProvider frameworkProvider)
        {
            var requestStore = frameworkProvider.HttpRequestStore;

            if (!requestStore.Contains(Constants.TabStorageKey))
            {
                requestStore.Set(Constants.TabStorageKey, new Dictionary<string, IDataStore>());
            }

            var tabStorage = requestStore.Get<IDictionary<string, IDataStore>>(Constants.TabStorageKey);

            if (!tabStorage.ContainsKey(tabName))
            {
                tabStorage.Add(tabName, new DictionaryDataStoreAdapter(new Dictionary<string, object>()));
            }

            return tabStorage[tabName];
        }

        private void ExecuteTabs(RuntimeEvent runtimeEvent, IFrameworkProvider frameworkProvider)
        {
            var runtimeContext = frameworkProvider.RuntimeContext;
            var frameworkProviderRuntimeContextType = runtimeContext.GetType();
            var messageBroker = Configuration.MessageBroker;

            // Only use tabs that either don't specify a specific context type, or have a context type that matches the current framework provider's.
            var runtimeTabs =
                Configuration.Tabs.Where(
                    tab =>
                    tab.RequestContextType == null ||
                    frameworkProviderRuntimeContextType.IsSubclassOf(tab.RequestContextType) ||
                    tab.RequestContextType == frameworkProviderRuntimeContextType);

            var supportedRuntimeTabs = runtimeTabs.Where(p => p.ExecuteOn.HasFlag(runtimeEvent));
            var tabResultsStore = GetTabResultsStore(frameworkProvider);
            var logger = Configuration.Logger;

            foreach (var tab in supportedRuntimeTabs)
            {
                TabResult result;
                var key = CreateKey(tab);
                try
                {
                    var tabContext = new TabContext(runtimeContext, GetTabStore(key, frameworkProvider), logger, messageBroker);
                    var tabData = tab.GetData(tabContext);

                    var tabSection = tabData as TabSection;
                    if (tabSection != null)
                    {
                        tabData = tabSection.Build();
                    }

                    result = new TabResult(tab.Name, tabData);
                }
                catch (Exception exception)
                {
                    result = new TabResult(tab.Name, exception.ToString());
                    logger.Error(Resources.ExecuteTabError, exception, key);
                }

                if (tabResultsStore.ContainsKey(key))
                {
                    tabResultsStore[key] = result;
                }
                else
                {
                    tabResultsStore.Add(key, result);
                }
            }
        }

        private void ExecuteDisplays(IFrameworkProvider frameworkProvider)
        {
            var runtimeContext = frameworkProvider.RuntimeContext;
            var messageBroker = Configuration.MessageBroker;

            var displayResultsStore = GetDisplayResultsStore(frameworkProvider);
            var logger = Configuration.Logger;

            foreach (var display in Configuration.Displays)
            {
                TabResult result; // TODO: Rename now that it is no longer *just* tab results
                var key = CreateKey(display);
                try
                {
                    var displayContext = new TabContext(runtimeContext, GetTabStore(key, frameworkProvider), logger, messageBroker); // TODO: Do we need a DisplayContext?
                    var displayData = display.GetData(displayContext);

                    result = new TabResult(display.Name, displayData);
                }
                catch (Exception exception)
                {
                    result = new TabResult(display.Name, exception.ToString());
                    logger.Error(Resources.ExecuteTabError, exception, key);
                }

                if (displayResultsStore.ContainsKey(key))
                {
                    displayResultsStore[key] = result;
                }
                else
                {
                    displayResultsStore.Add(key, result);
                }
            }
        }
        
        private void PersistMetadata()
        {
            var metadata = new GlimpseMetadata { Version = Version, Hash = Configuration.Hash };
            var tabMetadata = metadata.Tabs;

            foreach (var tab in Configuration.Tabs)
            {
                var metadataInstance = new TabMetadata();

                var documentationTab = tab as IDocumentation;
                if (documentationTab != null)
                {
                    metadataInstance.DocumentationUri = documentationTab.DocumentationUri;
                }

                var layoutControlTab = tab as ILayoutControl;
                if (layoutControlTab != null)
                {
                    metadataInstance.KeysHeadings = layoutControlTab.KeysHeadings;
                }

                var layoutTab = tab as ITabLayout;
                if (layoutTab != null)
                {
                    metadataInstance.Layout = layoutTab.GetLayout();
                }

                if (metadataInstance.HasMetadata)
                {
                    tabMetadata[CreateKey(tab)] = metadataInstance;
                } 
            }

            var resources = metadata.Resources;
            var endpoint = Configuration.ResourceEndpoint;
            var logger = Configuration.Logger;

            foreach (var resource in Configuration.Resources)
            {
                var resourceKey = CreateKey(resource);
                if (resources.ContainsKey(resourceKey))
                {
                    logger.Warn(Resources.GlimpseRuntimePersistMetadataMultipleResourceWarning, resource.Name);
                }

                resources[resourceKey] = endpoint.GenerateUriTemplate(resource, Configuration.EndpointBaseUri, logger);
            }

            Configuration.PersistenceStore.Save(metadata);
        }

        private RuntimePolicy GetRuntimePolicy(RuntimeEvent runtimeEvent, IFrameworkProvider frameworkProvider)
        {
            var requestStore = frameworkProvider.HttpRequestStore;
            
            // Begin with the lowest policy for this request, or the lowest policy per config
            var finalResult = requestStore.Contains(Constants.RuntimePolicyKey)
                             ? requestStore.Get<RuntimePolicy>(Constants.RuntimePolicyKey)
                             : Configuration.DefaultRuntimePolicy;

            if (!finalResult.HasFlag(RuntimePolicy.Off))
            {
                var logger = Configuration.Logger;
                
                // only run policies for this runtimeEvent, or all runtime events
                var policies =
                    Configuration.RuntimePolicies.Where(
                        policy => policy.ExecuteOn.HasFlag(runtimeEvent));

                var policyContext = new RuntimePolicyContext(frameworkProvider.RequestMetadata, Configuration.Logger, frameworkProvider.RuntimeContext);
                foreach (var policy in policies)
                {
                    var policyResult = RuntimePolicy.Off;
                    try
                    {
                        policyResult = policy.Execute(policyContext);

                        if (policyResult != RuntimePolicy.On)
                        {
                            logger.Debug("RuntimePolicy set to '{0}' by IRuntimePolicy of type '{1}' during RuntimeEvent '{2}'.", policyResult, policy.GetType(), runtimeEvent);
                        }
                    }
                    catch (Exception exception)
                    {
                        logger.Warn("Exception when executing IRuntimePolicy of type '{0}'. RuntimePolicy is now set to 'Off'.", exception, policy.GetType());
                    }

                    // Only use the lowest policy allowed for the request
                    if (policyResult < finalResult)
                    {
                        finalResult = policyResult;
                    }
                }
            }

            // store result for request
            requestStore.Set(Constants.RuntimePolicyKey, finalResult);
            return finalResult;
        }

        // TODO this should not be public! This was changed to hack in OWIN support
        public string GenerateScriptTags(Guid requestId, IFrameworkProvider frameworkProvider)
        {
            var requestStore = frameworkProvider.HttpRequestStore;
            var runtimePolicy = requestStore.Get<RuntimePolicy>(Constants.RuntimePolicyKey);
            var hasRendered = false;

            if (requestStore.Contains(Constants.ScriptsHaveRenderedKey))
            {
                hasRendered = requestStore.Get<bool>(Constants.ScriptsHaveRenderedKey);
            }

            if (hasRendered)
            {
                return string.Empty;
            }

            var encoder = Configuration.HtmlEncoder;
            var resourceEndpoint = Configuration.ResourceEndpoint;
            var clientScripts = Configuration.ClientScripts;
            var logger = Configuration.Logger;
            var resources = Configuration.Resources;

            var stringBuilder = new StringBuilder();

            foreach (var clientScript in clientScripts.OrderBy(cs => cs.Order))
            {
                var dynamicScript = clientScript as IDynamicClientScript;
                if (dynamicScript != null)
                {
                    try
                    {
                        var requestTokenValues = new Dictionary<string, string>
                                         {
                                             { ResourceParameter.RequestId.Name, requestId.ToString() },
                                             { ResourceParameter.VersionNumber.Name, Version },
                                             { ResourceParameter.Hash.Name, Configuration.Hash }
                                         };

                        var resourceName = dynamicScript.GetResourceName();
                        var resource = resources.FirstOrDefault(r => r.Name.Equals(resourceName, StringComparison.InvariantCultureIgnoreCase));

                        if (resource == null)
                        {
                            logger.Warn(Resources.RenderClientScriptMissingResourceWarning, clientScript.GetType(), resourceName);
                            continue;
                        }

                        var uriTemplate = resourceEndpoint.GenerateUriTemplate(resource, Configuration.EndpointBaseUri, logger);

                        var resourceParameterProvider = dynamicScript as IParameterValueProvider;

                        if (resourceParameterProvider != null)
                        {
                            resourceParameterProvider.OverrideParameterValues(requestTokenValues);
                        }

                        var template = SetParameters(new UriTemplate(uriTemplate), requestTokenValues);
                        var uri = encoder.HtmlAttributeEncode(template.Resolve());

                        if (!string.IsNullOrEmpty(uri))
                        {
                            stringBuilder.AppendFormat(@"<script type='text/javascript' src='{0}'></script>", uri);
                        }

                        continue;
                    }
                    catch (Exception exception)
                    {
                        logger.Error(Core.Resources.GenerateScriptTagsDynamicException, exception, dynamicScript.GetType());
                    }
                }

                var staticScript = clientScript as IStaticClientScript;
                if (staticScript != null)
                {
                    try
                    {
                        var uri = encoder.HtmlAttributeEncode(staticScript.GetUri(Version));

                        if (!string.IsNullOrEmpty(uri))
                        {
                            stringBuilder.AppendFormat(@"<script type='text/javascript' src='{0}'></script>", uri);
                        }

                        continue;
                    }
                    catch (Exception exception)
                    {
                        logger.Error(Core.Resources.GenerateScriptTagsStaticException, exception, staticScript.GetType());
                    }
                }

                logger.Warn(Core.Resources.RenderClientScriptImproperImplementationWarning, clientScript.GetType());
            }

            requestStore.Set(Constants.ScriptsHaveRenderedKey, true);
            return stringBuilder.ToString();
        }

        /// <summary>
        /// The message used to to track the beginning and end of Http requests.
        /// </summary>
        protected class RuntimeMessage : ITimelineMessage, ISourceMessage
        {
            /// <summary>
            /// Gets the id of the request.
            /// </summary>
            /// <value>
            /// The id.
            /// </value>
            public Guid Id { get; private set; }

            /// <summary>
            /// Gets or sets the name of the event.
            /// </summary>
            /// <value>
            /// The name of the event.
            /// </value>
            public string EventName { get; set; }

            /// <summary>
            /// Gets or sets the event category.
            /// </summary>
            /// <value>
            /// The event category.
            /// </value>
            public TimelineCategoryItem EventCategory { get; set; }

            /// <summary>
            /// Gets or sets the event sub text.
            /// </summary>
            /// <value>
            /// The event sub text.
            /// </value>
            public string EventSubText { get; set; }

            /// <summary>
            /// Gets or sets the type of the executed.
            /// </summary>
            /// <value>
            /// The type of the executed.
            /// </value>
            public Type ExecutedType { get; set; }

            /// <summary>
            /// Gets or sets the executed method.
            /// </summary>
            /// <value>
            /// The executed method.
            /// </value>
            public MethodInfo ExecutedMethod { get; set; }

            /// <summary>
            /// Gets or sets the offset.
            /// </summary>
            /// <value>
            /// The offset.
            /// </value>
            public TimeSpan Offset { get; set; }

            /// <summary>
            /// Gets or sets the duration.
            /// </summary>
            /// <value>
            /// The duration.
            /// </value>
            public TimeSpan Duration { get; set; }

            /// <summary>
            /// Gets or sets the start time.
            /// </summary>
            /// <value>
            /// The start time.
            /// </value>
            public DateTime StartTime { get; set; }
        }
    }
}