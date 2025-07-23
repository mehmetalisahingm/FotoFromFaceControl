//using Microsoft.AspNetCore.Http;
//using Microsoft.AspNetCore.Mvc;
//using System.Threading.Tasks;

//namespace YourNamespace.Controllers
//{
//    public class SelectedFeaturesRequest
//    {
//        /// <summary>
//        /// Yüz seçeneği (true ise yüz kırpılacak)
//        /// </summary>
//        public bool Face { get; set; }

//        /// <summary>
//        /// Burun seçeneği (true ise burun kırpılacak)
//        /// </summary>
//        public bool Nose { get; set; }

//        /// <summary>
//        /// Göz seçeneği (true ise gözler kırpılacak)
//        /// </summary>
//        public bool Eyes { get; set; }
//    }

//    [ApiController]
//    [Route("api/[controller]")]
//    public class SelectedFeaturesController : ControllerBase
//    {
//        [HttpPost("upload-and-process")]
//        public async Task<IActionResult> UploadAndProcess([FromForm] SelectedFeaturesRequest request, IFormFile file)
//        {
//            if (file == null || file.Length == 0)
//                return BadRequest("Dosya yüklenmedi.");

//            // Örnek çıktı için gelen seçimleri döndürelim
//            var selected = new
//            {
//                Face = request.Face,
//                Nose = request.Nose,
//                Eyes = request.Eyes
//            };

//            // Burada gerçek kırpma ve zipleme işlemini yapacaksın.

//            return Ok(selected);
//        }
//    }
//}
