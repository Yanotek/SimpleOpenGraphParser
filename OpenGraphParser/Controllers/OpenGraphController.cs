using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using OpenGraphNet;

namespace OpenGraphParser.Controllers
{
    [ApiController]
    [Route("[controller]/[action]")]
    public class OpenGraphController : ControllerBase
    {
        private readonly IMemoryCache _memoryCache;
        
        private static readonly List<string> NeededMetaTags = new()
        {
            "description",
            "title"
        };

        public OpenGraphController(IMemoryCache memoryCache)
        {
            _memoryCache = memoryCache;
        }

        [HttpGet]
        [EnableCors("MyPolicy")]
        public async Task<IActionResult> Parse(string url, string? userAgent = "", bool validate = true, int timeoutInMilliseconds = 10000)
        {
            try
            {
                if (_memoryCache.TryGetValue(url, out var cacheResult))
                {
                    return new JsonResult(cacheResult);
                }

                var graph = await OpenGraph.ParseUrlAsync(url, userAgent, validate, timeoutInMilliseconds);

                var result = new Dictionary<string, string>();
                foreach (var metadata in graph.Metadata)
                {
                    result.Add(metadata.Key, metadata.Value.FirstOrDefault()?.Value ?? "");
                }

                if (!result.Any())
                {
                    result = await ParseMetaTags(url);
                }

                _memoryCache.Set(url, result, new MemoryCacheEntryOptions()
                {
                    Size = 1,
                    SlidingExpiration = TimeSpan.FromHours(1)
                });
                
                return new JsonResult(result);
            }
            catch (Exception ex)
            {
                return BadRequest($"Unhandled exception: {ex.Message}");
            }
        }
        
        private async Task<Dictionary<string, string>> ParseMetaTags(string url)
        {
            var result = new Dictionary<string, string>();

            var web = new HtmlWeb();
            var doc = await web.LoadFromWebAsync(url);

            var titleNode = doc.DocumentNode.SelectSingleNode("//title");
            if (titleNode != null)
            {
                result.Add("title", titleNode.InnerText);
            }

            var nodes = doc.DocumentNode.SelectNodes("//meta");

            foreach (var node in nodes)
            {
                var contentAttribute = node.Attributes.FirstOrDefault(x => x.Name == "content")?.Value;
                var nameAttribute = node.Attributes.FirstOrDefault(x => x.Name == "name")?.Value?.ToLower();
                if (nameAttribute == null)
                {
                    continue;
                }

                if (NeededMetaTags.Contains(nameAttribute) && !result.ContainsKey(nameAttribute))
                {
                    result.Add(nameAttribute, contentAttribute ?? "");
                }
            }

            return result;
        }
    }
}