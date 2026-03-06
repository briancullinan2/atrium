
using OpenCvSharp;
using OpenCvSharp.Dnn;
using OpenCvSharp.ImgHash;
using System.Runtime.InteropServices;
using Size = OpenCvSharp.Size;

namespace StudySauce.Services
{
    internal class FingerPrint
    {
        private static Net? _net;

        public static string Image(string filePath)
        {
            using var orb = ORB.Create();
            using var img = Cv2.ImRead(filePath, ImreadModes.Grayscale); // Grayscale is faster/standard for ORB

            if (img.Empty()) throw new Exception("Could not read image file.");

            using var descriptors = new Mat();

            // 1. Extract Features
            orb.DetectAndCompute(img, null, out KeyPoint[] keyPoints, descriptors);

            if (descriptors.Empty()) return string.Empty;

            // 2. Serialize Mat to Byte Array
            // ORB descriptors are typically CV_8U (8-bit unsigned integers)
            int size = (int)(descriptors.Total() * descriptors.ElemSize());
            byte[] buffer = new byte[size];
            System.Runtime.InteropServices.Marshal.Copy(descriptors.Data, buffer, 0, size);

            // 3. Return as Base64 for your DB string column
            return Convert.ToBase64String(buffer);
        }

        public static bool IsVisualMatch(string base64_1, string base64_2)
        {
            if (string.IsNullOrEmpty(base64_1) || string.IsNullOrEmpty(base64_2)) return false;

            byte[] b1 = Convert.FromBase64String(base64_1);
            byte[] b2 = Convert.FromBase64String(base64_2);

            using var m1 = new Mat(b1.Length / 32, 32, MatType.CV_8U);
            using var m2 = new Mat(b2.Length / 32, 32, MatType.CV_8U);

            System.Runtime.InteropServices.Marshal.Copy(b1, 0, m1.Data, b1.Length);
            System.Runtime.InteropServices.Marshal.Copy(b2, 0, m2.Data, b2.Length);

            using var matcher = new BFMatcher(NormTypes.Hamming);
            DMatch[] matches = matcher.Match(m1, m2);

            // Same "Good Match" logic as before...
            return matches.Count(x => x.Distance < 50) > (m1.Rows * 0.8);
        }


        public static long Text(string text)
        {
            // 1. Tokenize and clean (Lowercase, remove punctuation)
            var tokens = text.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);

            // 2. Hash each token and weigh them
            int[] v = new int[64];
            foreach (var token in tokens)
            {
                long hash = GetFNV1aHash(token); // Standard 64-bit hash
                for (int i = 0; i < 64; i++)
                {
                    // Bitwise weighting
                    if (((hash >> i) & 1) == 1) v[i]++;
                    else v[i]--;
                }
            }

            // 3. Fingerprint construction
            long fingerprint = 0;
            for (int i = 0; i < 64; i++)
            {
                if (v[i] > 0) fingerprint |= (1L << i);
            }
            return fingerprint;
        }

        public static long GetFNV1aHash(string text)
        {
            // FNV-1a constants for 64-bit
            const ulong offsetBasis = 0xcbf29ce484222325;
            const ulong prime = 0x100000001b3;

            ulong hash = offsetBasis;

            // Use unchecked to allow the math to wrap around naturally
            unchecked
            {
                foreach (char c in text)
                {
                    // FNV-1a order: XOR then Multiply
                    hash ^= (ushort)c;
                    hash *= prime;
                }
            }

            // Return as long for database compatibility (BIGINT)
            return (long)hash;
        }

        static FingerPrint()
        {
            _net = CvDnn.ReadNetFromOnnx("wwwroot/models/feature_extractor.onnx");
        }

        public double VerifyAuthorship(Mat img1, Mat img2)
        {
            // 1. Load the pre-trained model (e.g., SqueezeNet or a ReID model)
            // You only need to do this once, ideally cached in a DI singleton
            if (_net == null)
            {
                return 0.0;
            }

            // 2. Pre-process images into Blobs (standardized size/mean for the model)
            using var blob1 = CvDnn.BlobFromImage(img1, 1.0 / 255, new Size(224, 224), new Scalar(0, 0, 0), true, false);
            using var blob2 = CvDnn.BlobFromImage(img2, 1.0 / 255, new Size(224, 224), new Scalar(0, 0, 0), true, false);

            // 3. Forward pass to get the feature vectors (embeddings)
            _net.SetInput(blob1);
            using var prob1 = _net.Forward();

            _net.SetInput(blob2);
            using var prob2 = _net.Forward();

            // 4. Calculate the Comparator (Distance)
            // We use L2 (Euclidean) as defined in your provided enum [CV_DIST_L2]
            double distance = Cv2.Norm(prob1, prob2, NormTypes.L2);

            // 5. Optional: Normalize or invert the score so 1.0 = Perfect Match, 0.0 = No Relation
            // For authorship, a distance < 0.5 is usually a strong match.
            return distance;
        }


        public byte[] GetImageFingerprint(Mat img)
        {
            using var hasher = PHash.Create();
            using var hash = new Mat();

            // PHash produces an 8-byte (CV_8U) result
            hasher.Compute(img, hash);

            // Copy data from the Mat's unmanaged pointer to a managed byte array
            byte[] bytes = new byte[8];
            Marshal.Copy(hash.Data, bytes, 0, 8);
            return bytes;
        }

        public double CompareFingerprints(byte[] hash1, byte[] hash2)
        {
            using var hasher = PHash.Create();

            // Use FromPixelData to create a Mat from your SQLite byte array
            using var mat1 = Mat.FromPixelData(1, 8, MatType.CV_8U, hash1);
            using var mat2 = Mat.FromPixelData(1, 8, MatType.CV_8U, hash2);

            // Returns the Hamming distance (0 = identical)
            return hasher.Compare(mat1, mat2);
        }
    }
}
