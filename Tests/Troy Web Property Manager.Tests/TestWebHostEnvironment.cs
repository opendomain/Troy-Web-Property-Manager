using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace Troy_Web_Property_Manager.Tests
{
    /// <summary>An environment with just a name, for code that behaves differently in Development.</summary>
    public sealed class TestWebHostEnvironment(string environmentName) : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = "";
        public string WebRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
