using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using NUnit.Framework;

namespace LLMClient.Tests.Integration
{
    [TestFixture]
    [Category("Integration")]
    public class EmbeddingComparisonTests2
    {
        private const string E5_DIR = @"C:\Users\hdtdt\AppData\Local\User Name\com.companyname.llmclient\Data\models\intfloat-e5-large-multilingual-v1";
        private const string GEMMA_DIR = @"C:\Users\hdtdt\AppData\Local\User Name\com.companyname.llmclient\Data\models\embeddinggemma-300m";
        
        private InferenceSession? _e5Session;
        private InferenceSession? _gemmaSession;
        private bool _e5Ok, _gemmaOk, _tokE5Ok, _tokGemmaOk;

        [OneTimeSetUp]
        public async Task Setup()
        {
            var e5Path = Path.Combine(E5_DIR, "model.onnx");
            if (File.Exists(e5Path))
            {
                try { _e5Session = new InferenceSession(e5Path); _e5Ok = true; } catch { }
            }
            var gemmaPath = Path.Combine(GEMMA_DIR, "onnx", "model.onnx");
            if (File.Exists(gemmaPath))
            {
                try { _gemmaSession = new InferenceSession(gemmaPath); _gemmaOk = true; } catch { }
            }
            // E5 tokenizer
            try {
                var tokPath = Path.Combine(E5_DIR, "tokenizer.json");
                if (File.Exists(tokPath)) { var r = await TokenizerNative.InitNamedAsync("e5", tokPath); _tokE5Ok = r == 0; }
            } catch { }
            // Gemma tokenizer
            try {
                var tokPath = Path.Combine(GEMMA_DIR, "tokenizer.json");
                TestContext.WriteLine($"Gemma tokenizer path: {tokPath}, exists: {File.Exists(tokPath)}");
                if (File.Exists(tokPath)) { 
                    var r = await TokenizerNative.InitNamedAsync("gemma", tokPath); 
                    _tokGemmaOk = r == 0;
                    TestContext.WriteLine($"Gemma tokenizer init: {(_tokGemmaOk ? "OK" : "FAILED")}");
                }
            } catch (Exception ex) { TestContext.WriteLine($"Gemma tokenizer error: {ex.Message}"); }
        }

        [OneTimeTearDown]
        public void Cleanup() { _e5Session?.Dispose(); _gemmaSession?.Dispose(); }

        private async Task<int[]> TokE5(string t) { 
            var ids = new int[512]; 
            var len = await TokenizerNative.EncodeNamedAsync("e5", t, ids, 512); 
            return ids.Take(Math.Max(len,1)).ToArray(); 
        }
        
        private async Task<int[]> TokGemma(string t) { 
            if (_tokGemmaOk) { 
                var ids = new int[512]; 
                var len = await TokenizerNative.EncodeNamedAsync("gemma", t, ids, 512); 
                return ids.Take(Math.Max(len,1)).ToArray(); 
            }
            // Hash fallback
            var toks = new List<int>{2}; 
            foreach(var c in t) toks.Add(((int)c*31+17)%250000+100); 
            toks.Add(1); 
            return toks.ToArray(); 
        }

        private float[] EmbE5(int[] ids)
        {
            if (_e5Session==null) return Array.Empty<float>();
            var inp = new long[512]; var mask = new long[512];
            for(int i=0;i<Math.Min(ids.Length,512);i++) { inp[i]=ids[i]; mask[i]=ids[i]!=0?1:0; }
            var inputs = new List<NamedOnnxValue> {
                NamedOnnxValue.CreateFromTensor("input_ids", new DenseTensor<long>(inp, new[]{1,512})),
                NamedOnnxValue.CreateFromTensor("attention_mask", new DenseTensor<long>(mask, new[]{1,512}))
            };
            using var res = _e5Session.Run(inputs);
            var t = res.First().AsTensor<float>(); var emb = new float[1024];
            for(int i=0;i<1024;i++) { float s=0; for(int j=0;j<Math.Min(ids.Length,512);j++) s+=t[0,j,i]; emb[i]=s/ids.Length; }
            return emb;
        }

        private float[] EmbGemma(int[] ids)
        {
            if (_gemmaSession==null) return Array.Empty<float>();
            var seqLen = Math.Min(ids.Length, 512);
            var inp = new long[seqLen]; var mask = new long[seqLen];
            for(int i=0;i<seqLen;i++) { inp[i]=ids[i]; mask[i]=ids[i]!=0?1:0; }
            var inputs = new List<NamedOnnxValue> {
                NamedOnnxValue.CreateFromTensor("input_ids", new DenseTensor<long>(inp, new[]{1,seqLen})),
                NamedOnnxValue.CreateFromTensor("attention_mask", new DenseTensor<long>(mask, new[]{1,seqLen}))
            };
            using var res = _gemmaSession.Run(inputs);
            return res.Last().AsTensor<float>().ToArray();
        }

        private float Cos(float[] a, float[] b)
        {
            if(a.Length==0||b.Length==0) return 0;
            float d=0,na=0,nb=0; for(int i=0;i<a.Length;i++) { d+=a[i]*b[i]; na+=a[i]*a[i]; nb+=b[i]*b[i]; }
            return d/(MathF.Sqrt(na)*MathF.Sqrt(nb)+1e-8f);
        }

        [Test]
        public async Task Compare_RealTokenizer_Polish_Similar()
        {
            if(!_e5Ok||!_gemmaOk||!_tokE5Ok) Assert.Ignore("Models not available");
            var pairs = new[] { ("Lubię programować.", "Uwielbiam kodować."), ("Kot śpi.", "Kotek drzemie."), ("Auto jedzie.", "Samochód pędzi.") };
            TestContext.WriteLine($"=== PL SIMILAR === (Gemma tokenizer: {(_tokGemmaOk?"REAL":"HASH")})");
            float e5t=0,gt=0;
            foreach(var (a,b) in pairs) {
                var e5s = Cos(EmbE5(await TokE5($"query: {a}")), EmbE5(await TokE5($"query: {b}")));
                var gs = Cos(EmbGemma(await TokGemma(a)), EmbGemma(await TokGemma(b)));
                e5t+=e5s; gt+=gs;
                TestContext.WriteLine($"E5={e5s:F3} Gemma={gs:F3} | {a} vs {b}");
            }
            TestContext.WriteLine($"AVG: E5={e5t/pairs.Length:F3}, Gemma={gt/pairs.Length:F3}");
        }

        [Test]
        public async Task Compare_RealTokenizer_Polish_Different()
        {
            if(!_e5Ok||!_gemmaOk||!_tokE5Ok) Assert.Ignore("Models not available");
            var pairs = new[] { ("Lubię programować.", "Pierogi są pyszne."), ("Kot śpi.", "Samolot leci."), ("Auto jedzie.", "Książka leży.") };
            TestContext.WriteLine($"=== PL DIFFERENT === (Gemma tokenizer: {(_tokGemmaOk?"REAL":"HASH")})");
            float e5t=0,gt=0;
            foreach(var (a,b) in pairs) {
                var e5s = Cos(EmbE5(await TokE5($"query: {a}")), EmbE5(await TokE5($"query: {b}")));
                var gs = Cos(EmbGemma(await TokGemma(a)), EmbGemma(await TokGemma(b)));
                e5t+=e5s; gt+=gs;
                TestContext.WriteLine($"E5={e5s:F3} Gemma={gs:F3} | {a} vs {b}");
            }
            TestContext.WriteLine($"AVG: E5={e5t/pairs.Length:F3}, Gemma={gt/pairs.Length:F3}");
        }

        [Test]
        public async Task Compare_RealTokenizer_CrossLingual()
        {
            if(!_e5Ok||!_gemmaOk||!_tokE5Ok) Assert.Ignore("Models not available");
            var pairs = new[] { ("Lubię programować.", "I like programming."), ("Kot jest zwierzęciem.", "A cat is an animal.") };
            TestContext.WriteLine($"=== CROSS-LINGUAL === (Gemma tokenizer: {(_tokGemmaOk?"REAL":"HASH")})");
            float e5t=0,gt=0;
            foreach(var (pl,en) in pairs) {
                var e5s = Cos(EmbE5(await TokE5($"query: {pl}")), EmbE5(await TokE5($"query: {en}")));
                var gs = Cos(EmbGemma(await TokGemma(pl)), EmbGemma(await TokGemma(en)));
                e5t+=e5s; gt+=gs;
                TestContext.WriteLine($"E5={e5s:F3} Gemma={gs:F3} | PL:{pl} EN:{en}");
            }
            TestContext.WriteLine($"AVG: E5={e5t/pairs.Length:F3}, Gemma={gt/pairs.Length:F3}");
        }
    }
}
