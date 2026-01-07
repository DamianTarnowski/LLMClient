using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using NUnit.Framework;

namespace LLMClient.Tests.Integration
{
    [TestFixture]
    [Category("Integration")]
    public class EmbeddingComparisonTests
    {
        private const string E5_DIR = @"C:\Users\hdtdt\AppData\Local\User Name\com.companyname.llmclient\Data\models\intfloat-e5-large-multilingual-v1";
        private const string GEMMA_DIR = @"C:\Users\hdtdt\AppData\Local\User Name\com.companyname.llmclient\Data\models\embeddinggemma-300m";
        
        private InferenceSession _e5Session;
        private InferenceSession _gemmaSession;
        private bool _e5Ok, _gemmaOk, _tokOk;

        [OneTimeSetUp]
        public async Task Setup()
        {
            var e5Path = Path.Combine(E5_DIR, "model.onnx");
            if (File.Exists(e5Path))
            {
                try { _e5Session = new InferenceSession(e5Path); _e5Ok = true; 
                    TestContext.WriteLine("E5 inputs: " + string.Join(", ", _e5Session.InputMetadata.Keys));
                } catch { }
            }
            var gemmaPath = Path.Combine(GEMMA_DIR, "onnx", "model.onnx");
            if (File.Exists(gemmaPath))
            {
                try { _gemmaSession = new InferenceSession(gemmaPath); _gemmaOk = true; 
                    TestContext.WriteLine("Gemma inputs: " + string.Join(", ", _gemmaSession.InputMetadata.Keys));
                } catch { }
            }
            try
            {
                var tokPath = Path.Combine(E5_DIR, "tokenizer.json");
                if (File.Exists(tokPath)) { var r = await TokenizerNative.InitNamedAsync("e5", tokPath); _tokOk = r == 0; }
            } catch { }
        }

        [OneTimeTearDown]
        public void Cleanup() { _e5Session?.Dispose(); _gemmaSession?.Dispose(); }

        private async Task<int[]> TokE5(string t) { var ids = new int[512]; var len = await TokenizerNative.EncodeNamedAsync("e5", t, ids, 512); return ids.Take(Math.Max(len,1)).ToArray(); }
        private int[] TokGemma(string t) { var toks = new List<int>{2}; foreach(var c in t) toks.Add(((int)c*31+17)%250000+100); toks.Add(1); while(toks.Count<64) toks.Add(0); return toks.Take(64).ToArray(); }

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
            var inp = new long[64]; var mask = new long[64];
            for(int i=0;i<Math.Min(ids.Length,64);i++) { inp[i]=ids[i]; mask[i]=ids[i]!=0?1:0; }
            var inputs = new List<NamedOnnxValue> {
                NamedOnnxValue.CreateFromTensor("input_ids", new DenseTensor<long>(inp, new[]{1,64})),
                NamedOnnxValue.CreateFromTensor("attention_mask", new DenseTensor<long>(mask, new[]{1,64}))
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
        public async Task Compare_Polish_Similar()
        {
            if(!_e5Ok||!_gemmaOk||!_tokE5Ok) Assert.Ignore("Not available");
            var pairs = new[] { ("Lubię programować.", "Uwielbiam kodować."), ("Kot śpi.", "Kotek drzemie."), ("Auto jedzie.", "Samochód pędzi.") };
            TestContext.WriteLine($"=== PL SIMILAR === (Gemma tok: {(_tokGemmaOk?"REAL":"HASH")})");
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
        public async Task Compare_Polish_Different()
        {
            if(!_e5Ok||!_gemmaOk||!_tokE5Ok) Assert.Ignore("Not available");
            var pairs = new[] { ("Lubię programować.", "Pierogi są pyszne."), ("Kot śpi.", "Samolot leci."), ("Auto jedzie.", "Książka leży.") };
            TestContext.WriteLine($"=== PL DIFFERENT === (Gemma tok: {(_tokGemmaOk?"REAL":"HASH")})");
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
        public async Task Compare_CrossLingual()
        {
            if(!_e5Ok||!_gemmaOk||!_tokE5Ok) Assert.Ignore("Not available");
            var pairs = new[] { ("Lubię programować.", "I like programming."), ("Kot jest zwierzęciem.", "A cat is an animal.") };
            TestContext.WriteLine($"=== CROSS-LINGUAL === (Gemma tok: {(_tokGemmaOk?"REAL":"HASH")})");
            float e5t=0,gt=0;
            foreach(var (pl,en) in pairs) {
                var e5s = Cos(EmbE5(await TokE5($"query: {pl}")), EmbE5(await TokE5($"query: {en}")));
                var gs = Cos(EmbGemma(await TokGemma(pl)), EmbGemma(await TokGemma(en)));
                e5t+=e5s; gt+=gs;
                TestContext.WriteLine($"E5={e5s:F3} Gemma={gs:F3} | PL:{pl} EN:{en}");
            }
            TestContext.WriteLine($"AVG: E5={e5t/pairs.Length:F3}, Gemma={gt/pairs.Length:F3}");
        }

        [Test]
        public async Task Compare_Performance()
        {
            if(!_e5Ok||!_gemmaOk||!_tokE5Ok) Assert.Ignore("Not available");
            var text = "Sztuczna inteligencja.";
            EmbE5(await TokE5($"query: {text}")); EmbGemma(await TokGemma(text));
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for(int i=0;i<3;i++) EmbE5(await TokE5($"query: {text}"));
            var e5t = sw.ElapsedMilliseconds/3.0;
            sw.Restart();
            for(int i=0;i<3;i++) EmbGemma(await TokGemma(text));
            var gt = sw.ElapsedMilliseconds/3.0;
            TestContext.WriteLine($"=== PERFORMANCE ===\nE5: {e5t:F0}ms\nGemma: {gt:F0}ms\nGemma {e5t/gt:F1}x faster");
        }
    }
}
