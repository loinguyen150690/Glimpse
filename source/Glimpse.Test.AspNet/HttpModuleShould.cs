using System;
using Glimpse.AspNet;
using Glimpse.Core.Framework;
using Glimpse.Test.AspNet.Tester;
using Xunit;
using Moq;

namespace Glimpse.Test.AspNet
{
    public class HttpModuleShould:IDisposable
    {
        private HttpModuleTester tester;

        private HttpModuleTester HttpModule
        {
            get { return tester ?? (tester = HttpModuleTester.Create()); }
            set { tester = value; }
        }

        public void Dispose()
        {
            HttpModule = null;
        }

        [Fact(Skip = "Fix to work with new init model.")]
        public void CallGlimpseRuntimeBeginRequestOnBeginRequest()
        {
            HttpModule.BeginRequest(HttpModule.ContextMock.Object);

            HttpModule.RuntimeMock.Verify(r=>r.BeginRequest(It.IsAny<IFrameworkProvider>()), Times.Once());
        }

        [Fact]
        public void DisposeNothing()
        {
            HttpModule.Dispose();
        }

        [Fact]
        public void LogOnAppDomainUnload()
        {
            var currentDomain = AppDomain.CurrentDomain;
            currentDomain.SetData(Constants.LoggerKey, HttpModule.LoggerMock.Object);

            HttpModule.UnloadDomain(currentDomain, null);

            HttpModule.LoggerMock.Verify(l => l.Fatal(It.IsAny<string>(), It.IsAny<object[]>()));
        }

        [Fact]
        public void SetAppDomainLoggerOnInit()
        {
            HttpModule.Init(HttpModule.AppMock.Object);

            Assert.NotNull(AppDomain.CurrentDomain.GetData(Constants.LoggerKey));
        }
    }
}