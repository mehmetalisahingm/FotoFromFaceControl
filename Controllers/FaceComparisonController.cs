//using DlibDotNet;
//using DlibDotNet.Extensions;
//using FotoFromFaceControl.Models;
//using Microsoft.AspNetCore.Http;
//using Microsoft.AspNetCore.Mvc;
//using System;
//using System.Collections.Generic;
//using System.Drawing;
//using System.IO;

//namespace FotoFromFaceControl.Controllers
//{
//    [ApiController]
//    [Route("api/[controller]")]
//    public class FaceCompareController : ControllerBase
//    {
//        private readonly string predictorPath;

//        public FaceCompareController(IWebHostEnvironment env)
//        {
//            predictorPath = Path.Combine(env.ContentRootPath, "cascades", "shape_predictor_68_face_landmarks.dat");
//        }

//        [HttpPost("compare")]
//        public IActionResult CompareFaces([FromForm] CompareFacesModel model)
//        {
//            if (model.File1 == null || model.File2 == null)
//                return BadRequest("İki görsel de yüklenmelidir.");

//            using var detector = Dlib.GetFrontalFaceDetector();
//            using var predictor = ShapePredictor.Deserialize(predictorPath);

//            var landmarks1 = GetLandmarksFromImage(model.File1, detector, predictor);
//            var landmarks2 = GetLandmarksFromImage(model.File2, detector, predictor);

//            if (landmarks1 == null || landmarks2 == null)
//                return BadRequest("Yüz tespiti yapılamadı.");

//            if (landmarks1.Count != landmarks2.Count)
//                return BadRequest("Landmark sayıları eşleşmiyor.");

//            double totalDistance = 0;
//            for (int i = 0; i < landmarks1.Count; i++)
//            {
//                double dx = landmarks1[i].X - landmarks2[i].X;
//                double dy = landmarks1[i].Y - landmarks2[i].Y;
//                totalDistance += Math.Sqrt(dx * dx + dy * dy);
//            }

//            double avgDistance = totalDistance / landmarks1.Count;
//            double similarity = Math.Max(0, 100 - avgDistance);

//            return Ok(new { similarity = Math.Round(similarity, 2) });
//        }

//        private List<PointF>? GetLandmarksFromImage(IFormFile file, FrontalFaceDetector detector, ShapePredictor predictor)
//        {
//            using var stream = file.OpenReadStream();
//            using var bitmap = new Bitmap(stream);
//            var array2D = bitmap.ToArray2D<RgbPixel>();

//            var faces = detector.Operator(array2D);
//            if (faces.Length == 0)
//                return null;

//            var shape = predictor.Detect(array2D, faces[0]);

//            var landmarks = new List<PointF>();
//            for (uint i = 0; i < shape.Parts; i++)
//            {
//                var point = shape.GetPart(i);
//                landmarks.Add(new PointF(point.X, point.Y));
//            }

//            return landmarks;
//        }
//    }
//}
