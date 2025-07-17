using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FotoFromFaceControl.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GalleryController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;

        public GalleryController(IWebHostEnvironment env)
        {
            _env = env;
        }

        [HttpGet("photos")]
        public IActionResult GetPhotos()
        {
            // uploads klasörünün fiziksel yolu
            string uploadsPath = Path.Combine(_env.WebRootPath, "uploads");

            if (!Directory.Exists(uploadsPath))
            {
                return Ok(new List<string>());
            }

            // jpg, png, jpeg uzantılı dosyaları al
            var files = Directory.GetFiles(uploadsPath)
                .Where(f => f.EndsWith(".jpg") || f.EndsWith(".jpeg") || f.EndsWith(".png"))
                .Select(f => "/uploads/" + Path.GetFileName(f))
                .ToList();

            return Ok(files);
        }
    }
}
