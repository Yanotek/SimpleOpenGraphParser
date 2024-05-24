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
        public async Task<IActionResult> Parse(string url, string? userAgent = "", bool validate = true, int timeoutInMilliseconds = 10000, bool bitchute = false)
        {
            try
            {
                var memoryKey = url + (bitchute ? "bitchute" : "");
                if (_memoryCache.TryGetValue(memoryKey, out var cacheResult))
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
                
                if (bitchute)
                {
                    var additionalData = await GetBitchuteAdditionalData(url, result);
                    _memoryCache.Set(memoryKey, additionalData, new MemoryCacheEntryOptions()
                    {
                        Size = 1,
                        SlidingExpiration = TimeSpan.FromHours(1)
                    });
                    return new JsonResult(additionalData);
                }

                _memoryCache.Set(memoryKey, result, new MemoryCacheEntryOptions()
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
        
        private async Task<Dictionary<string, object>> GetBitchuteAdditionalData(string url, Dictionary<string, string> ogData)
        {
            var result = new Dictionary<string, object>();
            result["og"] = ogData;

            var web = new HtmlWeb();
            var doc = await web.LoadFromWebAsync(url);

            var magnetNode = doc.DocumentNode.SelectSingleNode("//a[@title='Magnet Link']");
            string magnetLink = magnetNode?.Attributes["href"]?.Value;

            if (magnetLink != null && magnetLink.StartsWith("magnet"))
            {
                var videoData = new Dictionary<string, string>
                {
                    ["xt"] = ExtractMagnetParameter(magnetLink, "xt"),
                    ["dn"] = ExtractMagnetParameter(magnetLink, "dn"),
                    ["tr"] = ExtractMagnetParameter(magnetLink, "tr"),
                    ["as"] = ExtractMagnetParameter(magnetLink, "as"),
                    ["xs"] = ExtractMagnetParameter(magnetLink, "xs"),
                    ["title"] = ogData.ContainsKey("og:title") ? ogData["og:title"] : string.Empty,
                    ["preview"] = ogData.ContainsKey("og:image") ? ogData["og:image"] : string.Empty
                };
                result["video"] = videoData;
                result["magnet"] = magnetLink;
            }
            else
            {
                var sourceNode = doc.DocumentNode.SelectSingleNode("//video/source");
                var src = sourceNode?.Attributes["src"]?.Value;

                if (src != null)
                {
                    var videoData = new Dictionary<string, string>
                    {
                        ["as"] = src,
                        ["title"] = ogData.ContainsKey("og:title") ? ogData["og:title"] : string.Empty,
                        ["preview"] = ogData.ContainsKey("og:image") ? ogData["og:image"] : string.Empty
                    };
                    result["video"] = videoData;
                }
            }

            return result;
        }

        private string ExtractMagnetParameter(string magnetLink, string parameter)
        {
            var parameters = magnetLink
                .Substring(magnetLink.IndexOf('?') + 1)
                .Split('&')
                .Select(param => param.Split('='))
                .GroupBy(x => x[0])
                .Select(x => x.Last())
                .ToDictionary(parts => parts[0], parts => parts[1]);

            return parameters.TryGetValue(parameter, out var value) ? value : string.Empty;
        }
    }
}