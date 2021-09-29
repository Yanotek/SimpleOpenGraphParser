using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
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
    }
}