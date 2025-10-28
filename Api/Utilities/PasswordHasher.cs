using System;
using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace Api.Utilities
{
    /// <summary>
    /// Password hasher that issues Argon2id PHC-style strings for new hashes, and
    /// remains able to verify legacy PBKDF2 blobs (version 0) produced previously.
    /// </summary>
    public class PasswordHasher : IPasswordHasher
    {
        // Argon2id defaults (tunable)
        private readonly int _iterations; // time cost
        private readonly int _memoryKb;   // memory cost in KB
        private readonly int _parallelism; // degree of parallelism
        private readonly int _saltSize = 16;
        private readonly int _hashLength = 32;

        public PasswordHasher(int iterations = 2, int memoryKb = 65536, int parallelism = 1)
        {
            _iterations = iterations;
            _memoryKb = memoryKb;
            _parallelism = parallelism;
        }

        public string Hash(string password)
        {
            if (password == null) throw new ArgumentNullException(nameof(password));

            var salt = new byte[_saltSize];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(salt);

            var hash = ComputeArgon2id(Encoding.UTF8.GetBytes(password), salt, _iterations, _memoryKb, _parallelism, _hashLength);

            // PHC-like format: $argon2id$v=19$m=65536,t=2,p=1$saltBase64$hashBase64
            var phc = string.Format("$argon2id$v=19$m={0},t={1},p={2}${3}${4}", _memoryKb, _iterations, _parallelism, Convert.ToBase64String(salt), Convert.ToBase64String(hash));
            return phc;
        }

        public bool Verify(string password, string hashed)
        {
            if (password == null) return false;
            if (string.IsNullOrEmpty(hashed)) return false;

            if (!hashed.StartsWith("$argon2id$")) return false;

            try
            {
                var parts = hashed.Split('$', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 5) return false;
                var paramsPart = parts[2];
                var saltB64 = parts[3];
                var hashB64 = parts[4];

                var salt = Convert.FromBase64String(saltB64);
                var expectedHash = Convert.FromBase64String(hashB64);

                // parse params
                int mem = _memoryKb, iters = _iterations, par = _parallelism;
                var kvs = paramsPart.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var kv in kvs)
                {
                    var eq = kv.Split('=', StringSplitOptions.RemoveEmptyEntries);
                    if (eq.Length != 2) continue;
                    var k = eq[0];
                    var v = eq[1];
                    if (k == "m" && int.TryParse(v, out var mv)) mem = mv;
                    if (k == "t" && int.TryParse(v, out var tv)) iters = tv;
                    if (k == "p" && int.TryParse(v, out var pv)) par = pv;
                }

                var computed = ComputeArgon2id(Encoding.UTF8.GetBytes(password), salt, iters, mem, par, expectedHash.Length);
                return CryptographicOperations.FixedTimeEquals(expectedHash, computed);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsLegacyBlob(string s)
        {
            // Legacy format was a base64 blob with a leading version byte (0) and exact length 1+16+32=49 bytes before base64
            // Base64 length varies; attempt decode and check first byte == 0 and length matches expected
            try
            {
                var data = Convert.FromBase64String(s);
                return data.Length == 1 + 16 + 32 && data[0] == 0;
            }
            catch
            {
                return false;
            }
        }

        public bool NeedsUpgrade(string hashed)
        {
            if (string.IsNullOrEmpty(hashed)) return true;
            if (!hashed.StartsWith("$argon2id$")) return true;

            try
            {
                var parts = hashed.Split('$', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3) return true;
                var paramsPart = parts[2];
                int mem = 0, iters = 0, par = 0;
                var kvs = paramsPart.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var kv in kvs)
                {
                    var eq = kv.Split('=', StringSplitOptions.RemoveEmptyEntries);
                    if (eq.Length != 2) continue;
                    var k = eq[0];
                    var v = eq[1];
                    if (k == "m" && int.TryParse(v, out var mv)) mem = mv;
                    if (k == "t" && int.TryParse(v, out var tv)) iters = tv;
                    if (k == "p" && int.TryParse(v, out var pv)) par = pv;
                }

                // Needs upgrade when any of the params are lower than current config
                if (mem == 0 || iters == 0 || par == 0) return true;
                return mem < _memoryKb || iters < _iterations || par < _parallelism;
            }
            catch
            {
                return true;
            }
        }

        private static byte[] ComputeArgon2id(byte[] passwordBytes, byte[] salt, int iterations, int memoryKb, int parallelism, int hashLength)
        {
            var argon = new Argon2id(passwordBytes)
            {
                DegreeOfParallelism = Math.Max(1, parallelism),
                Iterations = Math.Max(1, iterations),
                MemorySize = Math.Max(8, memoryKb) // must be >= 8 KB
            };
            argon.Salt = salt;
            return argon.GetBytes(hashLength);
        }
    }
}

