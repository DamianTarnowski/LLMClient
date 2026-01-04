using System;
using System.IO;
class Test {
    static void Main() {
        var modelDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "User Name", "com.companyname.llmclient", "Data", "models", "embeddinggemma-300m");
        Console.WriteLine("ModelDir: " + modelDir);
        Console.WriteLine("Exists: " + Directory.Exists(modelDir));
        var onnxPath = Path.Combine(modelDir, "onnx", "model.onnx");
        Console.WriteLine("ONNX: " + onnxPath);
        Console.WriteLine("ONNX exists: " + File.Exists(onnxPath));
    }
}
