//using Emgu.CV;
//using Emgu.CV.Structure;
//using FotoFromFaceControl.Models;
//using Microsoft.AspNetCore.Mvc;
//using System.Drawing.Imaging;

//namespace FotoFromFaceControl
//{
//    [Route("api/[controller]")]
//    [ApiController]
//    public class DetectFaceFeaturesController : ControllerBase
//    {
//        [HttpPost("upload")]
//        [Consumes("multipart/form-data")]
//        public IActionResult UploadPhoto([FromForm] FileUploadModel photo)
//        {
//            if (photo == null || photo.File == null || photo.File.Length == 0)
//                return BadRequest("Fotoğraf yüklenmedi.");

//            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
//            if (!Directory.Exists(uploadsFolder))
//                Directory.CreateDirectory(uploadsFolder);

//            var uniqueFileName = $"{Guid.NewGuid()}_{photo.File.FileName}";
//            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

//            using (var stream = new FileStream(filePath, FileMode.Create))
//            {
//                photo.File.CopyTo(stream);
//            }

//            var url = $"{Request.Scheme}://{Request.Host}/uploads/{uniqueFileName}";
//            return Ok(new { url });
//        }

      
        
//    }
//}
