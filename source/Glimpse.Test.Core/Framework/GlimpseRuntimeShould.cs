using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Glimpse.Core;
using Glimpse.Core.Extensibility;
using Glimpse.Core.Extensions;
using Glimpse.Core.Framework;
using Glimpse.Test.Common;
using Glimpse.Test.Core.TestDoubles;
using Glimpse.Test.Core.Tester;
using Moq;
using Xunit;
using Xunit.Extensions;
using Glimpse.Test.Core.Extensions;

namespace Glimpse.Test.Core.Framework
{
    public class GlimpseRuntimeShould : IDisposable
    {
        private GlimpseRuntimeTester tester;
        public GlimpseRuntimeTester Runtime
        {
            get { return tester ?? (tester = GlimpseRuntimeTester.Create()); }
            set { tester = value; }
        }

        public void Dispose()
        {
            Runtime = null;
            GlimpseRuntime.Reset();
        }

        [Fact(Skip = "This test is hanging the test runner. Fix later")]
        public void SetRequestIdOnBeginRequest()
        {
            var providerMock = new Mock<IFrameworkProvider>().Setup();
            providerMock.Setup(fp => fp.HttpRequestStore).Returns(Runtime.HttpRequestStoreMock.Object);

            Runtime.BeginRequest(providerMock.Object);

            Runtime.HttpRequestStoreMock.Verify(store => store.Set(Constants.RequestIdKey, It.IsAny<Guid>()));
        }

        [Fact(Skip = "This test is hanging the test runner. Fix later")]
        public void StartGlobalStopwatchOnBeginRequest()
        {
            var providerMock = new Mock<IFrameworkProvider>().Setup();
            providerMock.Setup(fp => fp.HttpRequestStore).Returns(Runtime.HttpRequestStoreMock.Object);
            Runtime.BeginRequest(providerMock.Object);

            Runtime.HttpRequestStoreMock.Verify(store => store.Set(Constants.GlobalStopwatchKey, It.IsAny<Stopwatch>()));
        }

        [Fact(Skip = "This test is hanging the test runner. Fix later")]
        public void Construct()
        {
            Assert.False(string.IsNullOrWhiteSpace(GlimpseRuntime.Version));
        }

        [Fact(Skip = "This test is hanging the test runner. Fix later")]
        public void ThrowsExceptionIfEndRequestIsCalledBeforeBeginRequest()
        {
            var providerMock = new Mock<IFrameworkProvider>().Setup();

            //runtime.BeginRequest(); commented out on purpose for this test

            Assert.Throws<GlimpseException>(() => Runtime.EndRequest(providerMock.Object));
        }

        [Fact(Skip = "This test is hanging the test runner. Fix later")]
        public void ThrowsExceptionIfBeginRequestIsCalledBeforeInittialize()
        {
            var providerMock = new Mock<IFrameworkProvider>().Setup();

            //Runtime.Initialize();commented out on purpose for this test

            Assert.Throws<GlimpseException>(() => Runtime.BeginRequest(providerMock.Object));
        }

        [Fact(Skip = "This test is hanging the test runner. Fix later")]
        public void ExecutePluginsWithDefaultLifeCycle()
        {
            var providerMock = new Mock<IFrameworkProvider>().Setup();

            Runtime.Configuration.Tabs.Add(Runtime.TabMock.Object);

            Runtime.BeginRequest(providerMock.Object);
            Runtime.EndRequest(providerMock.Object);

            var results = providerMock.Object.HttpRequestStore.Get<IDictionary<string, TabResult>>(Constants.TabResultsDataStoreKey);
            Assert.NotNull(results);
            Assert.Equal(1, results.Count);

            Runtime.TabMock.Verify(p => p.GetData(It.IsAny<ITabContext>()), Times.Once());
        }

        [Fact(Skip = "This test is hanging the test runner. Fix later")]
        public void ExecutePluginsWithLifeCycleMismatch()
        {
            var providerMock = new Mock<IFrameworkProvider>().Setup();

            Runtime.TabMock.Setup(m => m.ExecuteOn).Returns(RuntimeEvent.EndRequest);

            Runtime.Configuration.Tabs.Add(Runtime.TabMock.Object);
            Runtime.BeginRequest(providerMock.Object);

            var results = providerMock.Object.HttpRequestStore.Get<IDictionary<string, TabResult>>(Constants.TabResultsDataStoreKey);
            Assert.NotNull(results);
            Assert.Equal(0, results.Count);

            Runtime.TabMock.Verify(p => p.GetData(It.IsAny<ITabContext>()), Times.Never());
        }

        [Fact(Skip = "This test is hanging the test runner. Fix later")]
        public void ExecutePluginsMakeSureNamesAreJsonSafe()
        {
            var providerMock = new Mock<IFrameworkProvider>().Setup();

            Runtime.Configuration.Tabs.Add(Runtime.TabMock.Object);
            Runtime.BeginRequest(providerMock.Object);
            Runtime.EndRequest(providerMock.Object);

            var results = providerMock.Object.HttpRequestStore.Get<IDictionary<string, TabResult>>(Constants.TabResultsDataStoreKey);
            Assert.NotNull(results);
            Assert.Equal(1, results.Count);
            Assert.Contains("castle_proxies_itabproxy", results.First().Key); 
        }

        [Fact(Skip = "This test is hanging the test runner. Fix later")]
        public void ExecutePluginsWithMatchingRuntimeContextType()
        {
            var providerMock = new Mock<IFrameworkProvider>().Setup();
            Runtime.Configuration.Tabs.Add(Runtime.TabMock.Object);
            Runtime.BeginRequest(providerMock.Object);
            Runtime.EndRequest(providerMock.Object);

            var results = providerMock.Object.HttpRequestStore.Get<IDictionary<string, TabResult>>(Constants.TabResultsDataStoreKey);
            Assert.NotNull(results);
            Assert.Equal(1, results.Count);

            Runtime.TabMock.Verify(p => p.GetData(It.IsAny<ITabContext>()), Times.Once());
        }

        [Fact(Skip = "This test is hanging the test runner. Fix later")]
        public void ExecutePluginsWithUnknownRuntimeContextType()
        {
            var providerMock = new Mock<IFrameworkProvider>().Setup();
            Runtime.TabMock.Setup(m => m.RequestContextType).Returns<Type>(null);

            Runtime.Configuration.Tabs.Add(Runtime.TabMock.Object);
            Runtime.BeginRequest(providerMock.Object);
            Runtime.EndRequest(providerMock.Object);

            var results = providerMock.Object.HttpRequestStore.Get<IDictionary<string, TabResult>>(Constants.TabResultsDataStoreKey);
            Assert.NotNull(results);
            Assert.Equal(1, results.Count);

            Runtime.TabMock.Verify(p => p.GetData(It.IsAny<ITabContext>()), Times.Once());
        }

        [Fact(Skip = "This test is hanging the test runner. Fix later")]
        public void ExecutePluginsWithDuplicateCollectionEntries()
        {
            var providerMock = new Mock<IFrameworkProvider>().Setup();
            //Insert the same plugin multiple times
            Runtime.Configuration.Tabs.Add(Runtime.TabMock.Object);
            Runtime.Configuration.Tabs.Add(Runtime.TabMock.Object);
            Runtime.Configuration.Tabs.Add(Runtime.TabMock.Object);
            Runtime.BeginRequest(providerMock.Object);
            Runtime.EndRequest(providerMock.Object);

            var results = providerMock.Object.HttpRequestStore.Get<IDictionary<string, TabResult>>(Constants.TabResultsDataStoreKey);
            Assert.NotNull(results);
            Assert.Equal(1, results.Count);

            Runtime.TabMock.Verify(p => p.GetData(It.IsAny<ITabContext>()), Times.AtLeastOnce());
        }

        [Fact(Skip = "This test is hanging the test runner. Fix later")]
        public void ExecutePluginThatFails()
        {
            var providerMock = new Mock<IFrameworkProvider>().Setup();
            Runtime.TabMock.Setup(p => p.GetData(It.IsAny<ITabContext>())).Throws<DummyException>();

            Runtime.Configuration.Tabs.Add(Runtime.TabMock.Object);
            Runtime.BeginRequest(providerMock.Object);
            Runtime.EndRequest(providerMock.Object);

            var results = providerMock.Object.HttpRequestStore.Get<IDictionary<string, TabResult>>(Constants.TabResultsDataStoreKey);
            Assert.NotNull(results);
            Assert.Equal(1, results.Count);

            Runtime.TabMock.Verify(p => p.GetData(It.IsAny<ITabContext>()), Times.Once());
            
            // Make sure the excption type above is logged here.
            Runtime.LoggerMock.Verify(l => l.Error(It.IsAny<string>(), It.IsAny<DummyException>()), Times.AtMost(Runtime.Configuration.Tabs.Count));
        }

        [Fact(Skip = "This test is hanging the test runner. Fix later")]
        public void ExecutePluginsWithEmptyCollection()
        {
            var providerMock = new Mock<IFrameworkProvider>().Setup();
            Runtime.Configuration.Tabs.Clear();
            Runtime.BeginRequest(providerMock.Object);
            Runtime.EndRequest(providerMock.Object);

            var results = providerMock.Object.HttpRequestStore.Get<IDictionary<string, TabResult>>(Constants.TabResultsDataStoreKey);
            Assert.NotNull(results);
            Assert.Equal(0, results.Count);
        }

        [Fact(Skip = "This test is hanging the test runner. Fix later")]
        public void FlagsTest()
        {
            //This test is just to help me keep my sanity with bitwise operators
            var support = RuntimeEvent.EndRequest;

            Assert.True(support.HasFlag(RuntimeEvent.EndRequest), "End is End");

            support = RuntimeEvent.EndRequest | RuntimeEvent.EndSessionAccess;

            Assert.True(support.HasFlag(RuntimeEvent.EndRequest), "End in End|SessionEnd");
            Assert.True(support.HasFlag(RuntimeEvent.EndSessionAccess), "SessionEnd in End|SessionEnd");
            //support End OR Begin
            Assert.True(support.HasFlag(RuntimeEvent.EndRequest & RuntimeEvent.BeginRequest), "End|Begin in End|SessionEnd");
            //support End AND SessionEnd
            Assert.True(support.HasFlag(RuntimeEvent.EndRequest | RuntimeEvent.EndSessionAccess), "End|SessionEnd in End|SessionEnd");
            Assert.False(support.HasFlag(RuntimeEvent.EndRequest | RuntimeEvent.BeginRequest), "End|Begin NOT in End|SessionEnd");
            Assert.False(support.HasFlag(RuntimeEvent.BeginRequest), "Begin NOT in End|SessionEnd");
            Assert.False(support.HasFlag(RuntimeEvent.BeginSessionAccess), "SessionBegin NOT in End|SessionEnd");
            Assert.False(support.HasFlag(RuntimeEvent.BeginRequest | RuntimeEvent.BeginSessionAccess), "Begin|SessionBegin NOT in End|SessionEnd");
        }

        [Fact(Skip = "This test is hanging the test runner. Fix later")]
        public void HaveASemanticVersion()
        {
            Version version;
            Assert.True(Version.TryParse(GlimpseRuntime.Version, out version));
            Assert.NotNull(version.Major);
            Assert.NotNull(version.Minor);
            Assert.NotNull(version.Build);
            Assert.Equal(-1, version.Revision);
        }

        [Fact(Skip = "This test is hanging the test runner. Fix later")]
        public void InitializeWithSetupTabs()
        {
            var setupMock = Runtime.TabMock.As<ITabSetup>();

            //one tab needs setup, the other does not
            Runtime.Configuration.Tabs.Add(Runtime.TabMock.Object);
            Runtime.Configuration.Tabs.Add(new DummyTab());

            setupMock.Verify(pm => pm.Setup(It.IsAny<ITabSetupContext>()), Times.Once());
        }

        [Fact(Skip = "This test is hanging the test runner. Fix later")]
        public void InitializeWithSetupTabThatFails()
        {
            var setupMock = Runtime.TabMock.As<ITabSetup>();
            setupMock.Setup(s => s.Setup(new Mock<ITabSetupContext>().Object)).Throws<DummyException>();

            //one tab needs setup, the other does not
            Runtime.Configuration.Tabs.Add(Runtime.TabMock.Object);
            Runtime.Configuration.Tabs.Add(new DummyTab());

            setupMock.Verify(pm => pm.Setup(It.IsAny<ITabSetupContext>()), Times.Once());
            Runtime.LoggerMock.Verify(l => l.Error(It.IsAny<string>(), It.IsAny<DummyException>()), Times.AtMost(Runtime.Configuration.Tabs.Count));
        }

        [Fact(Skip = "This test is hanging the test runner. Fix later")]
        public void InitializeWithInspectors()
        {
            Runtime.Configuration.Inspectors.Add(Runtime.InspectorMock.Object);

            Runtime.InspectorMock.Verify(pi => pi.Setup(It.IsAny<IInspectorContext>()), Times.Once());
        }

        [Fact(Skip = "This test is hanging the test runner. Fix later")]
        public void InitializeWithInspectorThatFails()
        {
            Runtime.InspectorMock.Setup(pi => pi.Setup(It.IsAny<IInspectorContext>())).Throws<DummyException>();

            Runtime.Configuration.Inspectors.Add(Runtime.InspectorMock.Object);

            Runtime.InspectorMock.Verify(pi => pi.Setup(It.IsAny<IInspectorContext>()), Times.Once());
            Runtime.LoggerMock.Verify(l => l.Error(It.IsAny<string>(), It.IsAny<DummyException>()), Times.AtMost(Runtime.Configuration.Inspectors.Count));
        }

        [Fact(Skip = "This test is hanging the test runner. Fix later")]
        public void InjectHttpResponseBodyDuringEndRequest()
        {
            var providerMock = new Mock<IFrameworkProvider>().Setup();

            Runtime.BeginRequest(providerMock.Object);
            Runtime.EndRequest(providerMock.Object);

            providerMock.Verify(fp => fp.InjectHttpResponseBody(It.IsAny<string>()));
        }

        [Fact(Skip = "This test is hanging the test runner. Fix later")]
        public void PersistDataDuringEndRequest()
        {
            var providerMock = new Mock<IFrameworkProvider>().Setup();
            Runtime.Configuration.Tabs.Add(Runtime.TabMock.Object);
            Runtime.BeginRequest(providerMock.Object);
            Runtime.EndRequest(providerMock.Object);

            Runtime.PersistenceStoreMock.Verify(ps => ps.Save(It.IsAny<GlimpseRequest>()));
        }

        [Fact(Skip = "This test is hanging the test runner. Fix later")]
        public void SetResponseHeaderDuringEndRequest()
        {
            var providerMock = new Mock<IFrameworkProvider>().Setup();
            Runtime.Configuration.Tabs.Add(Runtime.TabMock.Object);
            Runtime.BeginRequest(providerMock.Object);
            Runtime.EndRequest(providerMock.Object);

            providerMock.Verify(fp => fp.SetHttpResponseHeader(Constants.HttpResponseHeader, It.IsAny<string>()));
        }


        [Fact(Skip = "This test is hanging the test runner. Fix later")]
        public void ExecuteDefaultResource()
        {
            var providerMock = new Mock<IFrameworkProvider>().Setup();
            var name = "TestResource";
            Runtime.ResourceMock.Setup(r => r.Name).Returns(name);
            Runtime.ResourceMock.Setup(r => r.Execute(It.IsAny<IResourceContext>())).Returns(Runtime.ResourceResultMock.Object);
            Runtime.Configuration.Resources.Add(Runtime.ResourceMock.Object);
            Runtime.Configuration.DefaultResource = Runtime.ResourceMock.Object;

            Runtime.ExecuteDefaultResource(providerMock.Object);

            Runtime.ResourceMock.Verify(r => r.Execute(It.IsAny<IResourceContext>()), Times.Once());
            Runtime.ResourceResultMock.Verify(r => r.Execute(It.IsAny<IResourceResultContext>()));
        }

        [Fact(Skip = "This test is hanging the test runner. Fix later")]
        public void ExecuteResourceWithOrderedParameters()
        {
            var providerMock = new Mock<IFrameworkProvider>().Setup();
            var name = "TestResource";
            Runtime.ResourceMock.Setup(r => r.Name).Returns(name);
            Runtime.ResourceMock.Setup(r => r.Execute(It.IsAny<IResourceContext>())).Returns(Runtime.ResourceResultMock.Object);
            Runtime.Configuration.Resources.Add(Runtime.ResourceMock.Object);

            Runtime.ExecuteResource(providerMock.Object, name.ToLower(), new ResourceParameters(new[] { "One", "Two" }));

            Runtime.ResourceMock.Verify(r => r.Execute(It.IsAny<IResourceContext>()), Times.Once());
            Runtime.ResourceResultMock.Verify(r => r.Execute(It.IsAny<IResourceResultContext>()));
        }

        [Fact(Skip = "This test is hanging the test runner. Fix later")]
        public void ExecuteResourceWithNamedParameters()
        {
            var providerMock = new Mock<IFrameworkProvider>().Setup();
            var name = "TestResource";
            Runtime.ResourceMock.Setup(r => r.Name).Returns(name);
            Runtime.ResourceMock.Setup(r => r.Execute(It.IsAny<IResourceContext>())).Returns(Runtime.ResourceResultMock.Object);
            Runtime.Configuration.Resources.Add(Runtime.ResourceMock.Object);

            Runtime.ExecuteResource(providerMock.Object, name.ToLower(), new ResourceParameters(new Dictionary<string, string> { { "One", "1" }, { "Two", "2" } }));

            Runtime.ResourceMock.Verify(r => r.Execute(It.IsAny<IResourceContext>()), Times.Once());
            Runtime.ResourceResultMock.Verify(r => r.Execute(It.IsAny<IResourceResultContext>()));
        }

        [Fact(Skip = "This test is hanging the test runner. Fix later")]
        public void HandleUnknownResource()
        {
            var providerMock = new Mock<IFrameworkProvider>().Setup();
            Runtime.Configuration.Resources.Clear();

            Runtime.ExecuteResource(providerMock.Object, "random name that doesn't exist", new ResourceParameters(new string[]{}));

            providerMock.Verify(fp => fp.SetHttpResponseStatusCode(404), Times.Once());
        }

        [Fact(Skip = "This test is hanging the test runner. Fix later")]
        public void HandleDuplicateResources()
        {
            var providerMock = new Mock<IFrameworkProvider>().Setup();
            var name = "Duplicate";
            Runtime.ResourceMock.Setup(r => r.Name).Returns(name);

            Runtime.Configuration.Resources.Add(Runtime.ResourceMock.Object);
            Runtime.Configuration.Resources.Add(Runtime.ResourceMock.Object);

            Runtime.ExecuteResource(providerMock.Object, name, new ResourceParameters(new string[] { }));

            providerMock.Verify(fp => fp.SetHttpResponseStatusCode(500), Times.Once());
        }

        [Fact(Skip = "This test is hanging the test runner. Fix later")]
        public void ThrowExceptionWithEmptyResourceName()
        {
            var providerMock = new Mock<IFrameworkProvider>().Setup();
            Assert.Throws<ArgumentNullException>(() => Runtime.ExecuteResource(providerMock.Object, "", new ResourceParameters(new string[]{})));
        }

        [Fact(Skip = "This test is hanging the test runner. Fix later")]
        public void HandleResourcesThatThrowExceptions()
        {
            var providerMock = new Mock<IFrameworkProvider>().Setup();
            var name = "Anything";
            Runtime.ResourceMock.Setup(r => r.Name).Returns(name);
            Runtime.ResourceMock.Setup(r => r.Execute(It.IsAny<IResourceContext>())).Throws<Exception>();

            Runtime.Configuration.Resources.Add(Runtime.ResourceMock.Object);

            Runtime.ExecuteResource(providerMock.Object, name, new ResourceParameters(new string[] { }));

            providerMock.Verify(fp => fp.SetHttpResponseStatusCode(500), Times.Once());
        }

        [Fact(Skip = "This test is hanging the test runner. Fix later")]
        public void EnsureNullIsNotPassedToResourceExecute()
        {
            var providerMock = new Mock<IFrameworkProvider>().Setup();
            var name = "aName";
            Runtime.ResourceMock.Setup(r => r.Name).Returns(name);
            Runtime.ResourceMock.Setup(r => r.Execute(It.IsAny<IResourceContext>())).Returns(
                Runtime.ResourceResultMock.Object);

            Runtime.Configuration.Resources.Add(Runtime.ResourceMock.Object);

            Runtime.ExecuteResource(providerMock.Object, name, new ResourceParameters(new Dictionary<string, string>()));

            Runtime.ResourceMock.Verify(r => r.Execute(null), Times.Never());
        }

        [Fact(Skip = "This test is hanging the test runner. Fix later")]
        public void HandleResourceResultsThatThrowExceptions()
        {
            var providerMock = new Mock<IFrameworkProvider>().Setup();
            var name = "Anything";
            Runtime.ResourceMock.Setup(r => r.Name).Returns(name);
            Runtime.ResourceMock.Setup(r => r.Execute(It.IsAny<IResourceContext>())).Returns(Runtime.ResourceResultMock.Object);

            Runtime.ResourceResultMock.Setup(rr => rr.Execute(It.IsAny<IResourceResultContext>())).Throws<Exception>();

            Runtime.Configuration.Resources.Add(Runtime.ResourceMock.Object);

            Runtime.ExecuteResource(providerMock.Object, name, new ResourceParameters(new string[] { }));

            Runtime.LoggerMock.Verify(l => l.Fatal(It.IsAny<string>(), It.IsAny<Exception>(), It.IsAny<object[]>()), Times.Once());
        }

/*
        [Fact]
        public void ProvideLowestModeLevelOnInitializing()
        {
            var offPolicyMock = new Mock<IRuntimePolicy>();
            offPolicyMock.Setup(v => v.Execute(It.IsAny<IRuntimePolicyContext>())).Returns(RuntimePolicy.Off);
            offPolicyMock.Setup(v => v.ExecuteOn).Returns(RuntimeEvent.Initialize);

            Runtime.Configuration.RuntimePolicies.Add(Runtime.RuntimePolicyMock.Object);
            Runtime.Configuration.RuntimePolicies.Add(offPolicyMock.Object);

            var result = Runtime.Initialize();

            Assert.False(result);
        }
*/

/*
        [Fact]
        public void NotIncreaseModeOverLifetimeOfRequest()
        {
            var providerMock = new Mock<IFrameworkProvider>().Setup();
            var glimpseMode = RuntimePolicy.ModifyResponseBody;
            Runtime.Configuration.DefaultRuntimePolicy = glimpseMode;

            var firstMode = Runtime.Initialize();

            Assert.True(firstMode);

            Runtime.Configuration.DefaultRuntimePolicy = RuntimePolicy.On;

            Runtime.BeginRequest(providerMock.Object);

            Assert.Equal(glimpseMode, providerMock.Object.HttpRequestStore.Get(Constants.RuntimePolicyKey));
        }
*/


        [Fact(Skip = "This test is hanging the test runner. Fix later")]
        public void ValidateAtBeginRequest()
        {
            var providerMock = new Mock<IFrameworkProvider>().Setup();
            Runtime.RuntimePolicyMock.Setup(rp => rp.ExecuteOn).Returns(RuntimeEvent.BeginRequest);

            Runtime.Configuration.RuntimePolicies.Add(Runtime.RuntimePolicyMock.Object);
            Runtime.BeginRequest(providerMock.Object);

            Runtime.RuntimePolicyMock.Verify(v=>v.Execute(It.IsAny<IRuntimePolicyContext>()), Times.AtLeastOnce());
        }

/*
        [Fact]
        public void SkipEecutingInitializeIfGlimpseModeIfOff()
        {
            var providerMock = new Mock<IFrameworkProvider>().Setup();
            Runtime.Configuration.DefaultRuntimePolicy = RuntimePolicy.Off;
            
            Runtime.Initialize();
            
            Assert.Equal(RuntimePolicy.Off, providerMock.Object.HttpRequestStore.Get(Constants.RuntimePolicyKey));
        }
*/

/*
        [Fact] //False result means GlimpseMode == Off
        public void WriteCurrentModeToRequestState()
        {
            var providerMock = new Mock<IFrameworkProvider>().Setup();
            Runtime.RuntimePolicyMock.Setup(v => v.Execute(It.IsAny<IRuntimePolicyContext>())).Returns(RuntimePolicy.ModifyResponseBody);
            Runtime.Configuration.RuntimePolicies.Add(Runtime.RuntimePolicyMock.Object);

            var result = Runtime.Initialize();

            Assert.True(result);

            Assert.Equal(RuntimePolicy.ModifyResponseBody, providerMock.Object.HttpRequestStore.Get(Constants.RuntimePolicyKey));
        }
*/

/*
        [Fact]
        public void SkipInitializeIfGlipseModeIsOff()
        {
            var providerMock = new Mock<IFrameworkProvider>().Setup();
            Runtime.Configuration.DefaultRuntimePolicy = RuntimePolicy.Off;

            Runtime.Initialize();

            Assert.Equal(RuntimePolicy.Off, providerMock.Object.HttpRequestStore.Get(Constants.RuntimePolicyKey));
        }
*/

        [Fact(Skip = "This test is hanging the test runner. Fix later")]
        public void SkipExecutingResourceIfGlipseModeIsOff()
        {
            var providerMock = new Mock<IFrameworkProvider>().Setup();
            Runtime.Configuration.DefaultRuntimePolicy = RuntimePolicy.Off;

            Runtime.ExecuteResource(providerMock.Object, "doesn't matter", new ResourceParameters(new string[]{}));

            Assert.Equal(RuntimePolicy.Off, providerMock.Object.HttpRequestStore.Get(Constants.RuntimePolicyKey));
        }

        [Fact(Skip = "This test is hanging the test runner. Fix later")]
        public void ValidateAtEndRequest()
        {
            var providerMock = new Mock<IFrameworkProvider>().Setup();
            Runtime.Configuration.DefaultRuntimePolicy = RuntimePolicy.Off;

            Runtime.EndRequest(providerMock.Object);

            Assert.Equal(RuntimePolicy.Off, providerMock.Object.HttpRequestStore.Get(Constants.RuntimePolicyKey));
        }

/*
        [Fact]
        public void ExecuteOnlyTheProperValidators()
        {
            var validatorMock2 = new Mock<IRuntimePolicy>();
            Runtime.Configuration.RuntimePolicies.Add(Runtime.RuntimePolicyMock.Object);
            Runtime.Configuration.RuntimePolicies.Add(validatorMock2.Object);

            Runtime.Initialize();

            Runtime.RuntimePolicyMock.Verify(v=>v.Execute(It.IsAny<IRuntimePolicyContext>()), Times.Once());
            validatorMock2.Verify(v=>v.Execute(It.IsAny<IRuntimePolicyContext>()), Times.Never());
        }
*/

/*        [Fact]
        public void GenerateNoScriptTagsWithoutClientScripts()
        {
            Assert.Equal("", Runtime.GenerateScriptTags(Guid.NewGuid()));
            
            Runtime.LoggerMock.Verify(l=>l.Warn(It.IsAny<string>()), Times.Never());
        }

        [Fact]
        public void GenerateNoScriptTagsAndWarnWithOnlyIClientScriptImplementations()
        {
            var clientScriptMock = new Mock<IClientScript>();
            clientScriptMock.Setup(cs => cs.Order).Returns(ScriptOrder.ClientInterfaceScript);

            Runtime.Configuration.ClientScripts.Add(clientScriptMock.Object);

            Assert.Equal("", Runtime.GenerateScriptTags(Guid.NewGuid()));
            
            Runtime.LoggerMock.Verify(l => l.Warn(It.IsAny<string>(), It.IsAny<object[]>()), Times.Once());
        }

        [Fact]
        public void GenerateScriptTagWithOneStaticResource()
        {
            var uri = "http://localhost/static";
            Runtime.StaticScriptMock.Setup(ss => ss.GetUri(GlimpseRuntime.Version)).Returns(uri);
            Runtime.EncoderMock.Setup(e => e.HtmlAttributeEncode(uri)).Returns(uri + "/encoded");

            Runtime.Configuration.ClientScripts.Add(Runtime.StaticScriptMock.Object);

            var result = Runtime.GenerateScriptTags(Guid.NewGuid());

            Assert.Contains(uri, result);
        }

        [Fact]
        public void GenerateScriptTagsInOrder()
        {
            var callCount = 0;
            //Lightweight call sequence checking idea from http://dpwhelan.com/blog/software-development/moq-sequences/
            Runtime.DynamicScriptMock.Setup(ds => ds.GetResourceName()).Returns("http://localhost/dynamic").Callback(()=>Assert.Equal(callCount++, 1));
            Runtime.StaticScriptMock.Setup(ss => ss.GetUri(GlimpseRuntime.Version)).Returns("http://localhost/static").Callback(()=>Assert.Equal(callCount++, 0));
            Runtime.EncoderMock.Setup(e => e.HtmlAttributeEncode("http://localhost/static")).Returns("http://localhost/static/encoded");

            Runtime.Configuration.ClientScripts.Add(Runtime.StaticScriptMock.Object);
            Runtime.Configuration.ClientScripts.Add(Runtime.DynamicScriptMock.Object);

            Assert.NotEmpty(Runtime.GenerateScriptTags(Guid.NewGuid()));
        }

        [Fact]
        public void GenerateScriptTagsWithParameterValueProvider()
        {
            var resourceName = "resourceName";
            var uri = "http://somethingEncoded";
            Runtime.ResourceMock.Setup(r => r.Name).Returns(resourceName);
            Runtime.DynamicScriptMock.Setup(ds => ds.GetResourceName()).Returns(resourceName);
            var parameterValueProviderMock = Runtime.DynamicScriptMock.As<IParameterValueProvider>();
            Runtime.EndpointConfigMock.Protected().Setup<string>("GenerateUriTemplate", resourceName, "~/Glimpse.axd", ItExpr.IsAny<IEnumerable<ResourceParameterMetadata>>(), ItExpr.IsAny<ILogger>()).Returns("http://something");
            Runtime.EncoderMock.Setup(e => e.HtmlAttributeEncode("http://something")).Returns(uri);

            Runtime.Configuration.Resources.Add(Runtime.ResourceMock.Object);
            Runtime.Configuration.ClientScripts.Add(Runtime.DynamicScriptMock.Object);

            Assert.Contains(uri, Runtime.GenerateScriptTags(Guid.NewGuid()));

            parameterValueProviderMock.Verify(vp=>vp.OverrideParameterValues(It.IsAny<IDictionary<string,string>>()));
        }

        [Fact]
        public void GenerateScriptTagsWithDynamicScriptAndMatchingResource()
        {
            var resourceName = "resourceName";
            var uri = "http://somethingEncoded";
            Runtime.ResourceMock.Setup(r => r.Name).Returns(resourceName);
            Runtime.DynamicScriptMock.Setup(ds => ds.GetResourceName()).Returns(resourceName);
            Runtime.EndpointConfigMock.Protected().Setup<string>("GenerateUriTemplate", resourceName, "~/Glimpse.axd", ItExpr.IsAny<IEnumerable<ResourceParameterMetadata>>(), ItExpr.IsAny<ILogger>()).Returns("http://something");
            Runtime.EncoderMock.Setup(e => e.HtmlAttributeEncode("http://something")).Returns(uri);

            Runtime.Configuration.Resources.Add(Runtime.ResourceMock.Object);
            Runtime.Configuration.ClientScripts.Add(Runtime.DynamicScriptMock.Object);

            Assert.Contains(uri, Runtime.GenerateScriptTags(Guid.NewGuid()));

            Runtime.ResourceMock.Verify(rm=>rm.Name, Times.AtLeastOnce());
            Runtime.EndpointConfigMock.Protected().Verify<string>("GenerateUriTemplate", Times.Once(), resourceName, "~/Glimpse.axd", ItExpr.IsAny<IEnumerable<ResourceParameterMetadata>>(), ItExpr.IsAny<ILogger>());
            Runtime.EncoderMock.Verify(e => e.HtmlAttributeEncode("http://something"), Times.Once());
        }

        [Fact]
        public void GenerateScriptTagsSkipsWhenEndpointConfigReturnsEmptyString()
        {
            var resourceName = "resourceName";
            Runtime.ResourceMock.Setup(r => r.Name).Returns(resourceName);
            Runtime.DynamicScriptMock.Setup(ds => ds.GetResourceName()).Returns(resourceName);
            Runtime.EndpointConfigMock.Protected().Setup<string>("GenerateUriTemplate", resourceName, "~/Glimpse.axd", ItExpr.IsAny<IEnumerable<ResourceParameterMetadata>>(), ItExpr.IsAny<ILogger>()).Returns("");
            Runtime.EncoderMock.Setup(e => e.HtmlAttributeEncode("")).Returns("");

            Runtime.Configuration.Resources.Add(Runtime.ResourceMock.Object);
            Runtime.Configuration.ClientScripts.Add(Runtime.DynamicScriptMock.Object);

            Assert.Empty(Runtime.GenerateScriptTags(Guid.NewGuid()));

            Runtime.ResourceMock.Verify(rm => rm.Name, Times.AtLeastOnce());
            Runtime.EndpointConfigMock.Protected().Verify<string>("GenerateUriTemplate", Times.Once(), resourceName, "~/Glimpse.axd", ItExpr.IsAny<IEnumerable<ResourceParameterMetadata>>(), ItExpr.IsAny<ILogger>());
            Runtime.EncoderMock.Verify(e => e.HtmlAttributeEncode(""), Times.Once());
        }

        [Fact]
        public void GenerateScriptTagsSkipsWhenMatchingResourceNotFound()
        {
            Runtime.DynamicScriptMock.Setup(ds => ds.GetResourceName()).Returns("resourceName");

            Runtime.Configuration.ClientScripts.Add(Runtime.DynamicScriptMock.Object);

            Assert.Empty(Runtime.GenerateScriptTags(Guid.NewGuid()));

            Runtime.LoggerMock.Verify(l => l.Warn(It.IsAny<string>(), It.IsAny<object[]>()));
        }

        [Fact]
        public void GenerateScriptTagsSkipsWhenStaticScriptReturnsEmptyString()
        {
            Runtime.StaticScriptMock.Setup(ss => ss.GetUri(GlimpseRuntime.Version)).Returns("");

            Runtime.Configuration.ClientScripts.Add(Runtime.StaticScriptMock.Object);

            Assert.Empty(Runtime.GenerateScriptTags(Guid.NewGuid()));
        }*/

        [Fact(Skip = "This test is hanging the test runner. Fix later")]
        public void LogErrorOnPersistenceStoreException()
        {
            var providerMock = new Mock<IFrameworkProvider>().Setup();
            Runtime.PersistenceStoreMock.Setup(ps => ps.Save(It.IsAny<GlimpseRequest>())).Throws<DummyException>();

            Runtime.BeginRequest(providerMock.Object);
            Runtime.EndRequest(providerMock.Object);

            Runtime.LoggerMock.Verify(l => l.Error(It.IsAny<string>(), It.IsAny<DummyException>(), It.IsAny<object[]>()));
        }

/*
        [Fact]
        public void LogWarningWhenRuntimePolicyThrowsException()
        {
            Runtime.RuntimePolicyMock.Setup(pm => pm.Execute(It.IsAny<IRuntimePolicyContext>())).Throws<DummyException>();

            Runtime.Configuration.RuntimePolicies.Add(Runtime.RuntimePolicyMock.Object);

            Assert.False(Runtime.Initialize());

            Runtime.LoggerMock.Verify(l => l.Warn(It.IsAny<string>(), It.IsAny<DummyException>(), It.IsAny<object[]>()));
        }
*/

        [Fact(Skip = "This test is hanging the test runner. Fix later")]
        public void LogErrorWhenDynamicScriptTagThrowsException()
        {
            var providerMock = new Mock<IFrameworkProvider>().Setup();
            Runtime.DynamicScriptMock.Setup(ds => ds.GetResourceName()).Throws<DummyException>();

            Runtime.Configuration.ClientScripts.Add(Runtime.DynamicScriptMock.Object);

            Runtime.BeginRequest(providerMock.Object);
            Runtime.EndRequest(providerMock.Object);

            Runtime.LoggerMock.Verify(l => l.Error(It.IsAny<string>(), It.IsAny<DummyException>(), It.IsAny<object[]>()));
        }


        [Fact(Skip = "This test is hanging the test runner. Fix later")]
        public void LogErrorWhenStaticScriptTagThrowsException()
        {
            var providerMock = new Mock<IFrameworkProvider>().Setup();
            Runtime.StaticScriptMock.Setup(ds => ds.GetUri(It.IsAny<string>())).Throws<DummyException>();

            Runtime.Configuration.ClientScripts.Add(Runtime.StaticScriptMock.Object);

            Runtime.BeginRequest(providerMock.Object);
            Runtime.EndRequest(providerMock.Object);

            Runtime.LoggerMock.Verify(l => l.Error(It.IsAny<string>(), It.IsAny<DummyException>(), It.IsAny<object[]>()));
        }

        [Fact(Skip = "This test is hanging the test runner. Fix later")]
        public void BeginRuntimeReturnsEarlyIfRuntimePolicyIsOff()
        {
            var providerMock = new Mock<IFrameworkProvider>().Setup();
            providerMock.Setup(fp => fp.HttpRequestStore).Returns(Runtime.HttpRequestStoreMock.Object);


            Runtime.RuntimePolicyMock.Setup(p => p.Execute(It.IsAny<IRuntimePolicyContext>())).Returns(RuntimePolicy.Off);
            Runtime.RuntimePolicyMock.Setup(p => p.ExecuteOn).Returns(RuntimeEvent.BeginRequest);
            Runtime.Configuration.RuntimePolicies.Add(Runtime.RuntimePolicyMock.Object);

            Runtime.BeginRequest(providerMock.Object);

            Runtime.HttpRequestStoreMock.Verify(fp=>fp.Set(Constants.RequestIdKey, It.IsAny<Guid>()), Times.Never());
        }

        [Fact(Skip = "This test is hanging the test runner. Fix later")]
        public void ExecuteTabsOnBeginSessionAccess()
        {
            var providerMock = new Mock<IFrameworkProvider>().Setup();
            Runtime.TabMock.Setup(t => t.ExecuteOn).Returns(RuntimeEvent.BeginSessionAccess);
            Runtime.Configuration.Tabs.Add(Runtime.TabMock.Object);

            Runtime.BeginRequest(providerMock.Object);
            Runtime.BeginSessionAccess(providerMock.Object);

            Runtime.TabMock.Verify(t=>t.GetData(It.IsAny<ITabContext>()), Times.Once());
        }

        [Fact(Skip = "This test is hanging the test runner. Fix later")]
        public void ExecuteTabsOnEndSessionAccess()
        {
            var providerMock = new Mock<IFrameworkProvider>().Setup();
            Runtime.TabMock.Setup(t => t.ExecuteOn).Returns(RuntimeEvent.EndSessionAccess);
            Runtime.Configuration.Tabs.Add(Runtime.TabMock.Object);

            Runtime.BeginRequest(providerMock.Object);
            Runtime.EndSessionAccess(providerMock.Object);

            Runtime.TabMock.Verify(t => t.GetData(It.IsAny<ITabContext>()), Times.Once());
        }

        [Fact(Skip = "This test is hanging the test runner. Fix later")]
        public void StopBeginSessionAccessWithRuntimePolicyOff()
        {
            var providerMock = new Mock<IFrameworkProvider>().Setup();
            Runtime.Configuration.DefaultRuntimePolicy = RuntimePolicy.Off;
            Runtime.TabMock.Setup(t => t.ExecuteOn).Returns(RuntimeEvent.BeginSessionAccess);
            Runtime.Configuration.Tabs.Add(Runtime.TabMock.Object);

            Runtime.BeginSessionAccess(providerMock.Object);

            Runtime.TabMock.Verify(t=>t.GetData(It.IsAny<ITabContext>()), Times.Never());
        }

        [Fact(Skip = "This test is hanging the test runner. Fix later")]
        public void StopEndSessionAccessWithRuntimePolicyOff()
        {
            var providerMock = new Mock<IFrameworkProvider>().Setup();
            Runtime.Configuration.DefaultRuntimePolicy = RuntimePolicy.Off;
            Runtime.TabMock.Setup(t => t.ExecuteOn).Returns(RuntimeEvent.EndSessionAccess);
            Runtime.Configuration.Tabs.Add(Runtime.TabMock.Object);

            Runtime.EndSessionAccess(providerMock.Object);

            Runtime.TabMock.Verify(t => t.GetData(It.IsAny<ITabContext>()), Times.Never());
        }

        [Fact(Skip = "This test is hanging the test runner. Fix later")]
        public void ThrowExceptionWhenExecutingResourceWithNullParameters()
        {
            var providerMock = new Mock<IFrameworkProvider>().Setup();
            Assert.Throws<ArgumentNullException>(() => Runtime.ExecuteResource(providerMock.Object, "any", null));
        }

        [Fact(Skip = "This test is hanging the test runner. Fix later")]
        public void ThrowExceptionWhenAccessingNonInitializedInstance()
        {
            Assert.Throws<GlimpseException>(() => GlimpseRuntime.Instance);
        }

        [Theory(Skip = "This test is hanging the test runner. Fix later"), AutoMock]
        public void InitializeSetsInstanceWhenExecuted(IGlimpseConfiguration configuration)
        {
            GlimpseRuntime.Initialize(configuration);

            Assert.NotNull(GlimpseRuntime.Instance);
        }

        [Theory(Skip = "This test is hanging the test runner. Fix later"), AutoMock]
        public void InitializeSetsConfigurationWhenExecuted(IGlimpseConfiguration configuration)
        {
            GlimpseRuntime.Initialize(configuration);

            Assert.Equal(configuration, GlimpseRuntime.Instance.Configuration);
        }

        [Fact(Skip = "This test is hanging the test runner. Fix later")]
        public void InitializeThrowsWithNullConfiguration()
        {
            Assert.Throws<ArgumentNullException>(() => GlimpseRuntime.Initialize(null));
        }
    }
}