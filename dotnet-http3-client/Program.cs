using System.Net;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.IO;

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (s, e) => cts.Cancel();


using var handler = new HttpClientHandler();
handler.ServerCertificateCustomValidationCallback = ServerCertificateCustomValidation;

using var client = new HttpClient(handler);

// client.DefaultRequestVersion = HttpVersion.Version30;
// client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;

Utils ut = new Utils();
ut.FileName = "./project.exe"; // hard coded for demo  
ut.TempFolder = @"./Temp";
ut.MaxFileSizeMB = 1;
ut.SplitFile();
System.Console.WriteLine(ut.FileParts.Count);

foreach (string file in ut.FileParts)
{
    UploadFile(file);
    File.Delete(file);
    System.Console.WriteLine(file);
}

bool UploadFile(string FileName)
{
    bool rslt = false;
    using var uploadHandler = new HttpClientHandler();
    uploadHandler.ServerCertificateCustomValidationCallback = ServerCertificateCustomValidation;
    using (var client = new HttpClient(handler))
    {
        using (var content = new MultipartFormDataContent())
        {
            var fileContent = new ByteArrayContent(System.IO.File.ReadAllBytes(FileName));
            fileContent.Headers.ContentDisposition = new
                ContentDispositionHeaderValue("attachment")
            {
                FileName = Path.GetFileName(FileName)
            };
            content.Add(fileContent);

            var requestUri = "https://localhost:5001";
            try
            {
                var result = client.PostAsync(requestUri, content).GetAwaiter().GetResult();
                rslt = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                // log error  
                rslt = false;
            }
        }
    }
    return rslt;
}
/*
This part contains tne original hello world
*/
var resp = await client.GetAsync("https://localhost:5001");
var body = await resp.Content.ReadAsStringAsync();

// Console.WriteLine($"Status: {resp.StatusCode}, version: {resp.Version}");

// Console.WriteLine();
// Console.WriteLine();
// Console.WriteLine();
// Console.WriteLine("Waiting 1 second before next request ...");
// await Task.Delay(1000, cts.Token);

bool ServerCertificateCustomValidation(HttpRequestMessage requestMessage, X509Certificate2? certificate, X509Chain? chain, SslPolicyErrors sslErrors)
{
    // It is possible inpect the certificate provided by server
    Console.WriteLine($"Requested URI: {requestMessage.RequestUri}");
    Console.WriteLine($"Effective date: {certificate?.GetEffectiveDateString()}");
    Console.WriteLine($"Exp date: {certificate?.GetExpirationDateString()}");
    Console.WriteLine($"Issuer: {certificate?.Issuer}");
    Console.WriteLine($"Subject: {certificate?.Subject}");

    // Based on the custom logic it is possible to decide whether the client considers certificate valid or not
    Console.WriteLine("========================================");
    var consoleColor = Console.ForegroundColor;
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"Errors: {sslErrors}");
    Console.ForegroundColor = consoleColor;
    Console.WriteLine("========================================");

    // return sslErrors == SslPolicyErrors.None;
    return true;
}

class Utils
{
    public string FileName { get; set; }
    public string TempFolder { get; set; }
    public int MaxFileSizeMB { get; set; }
    public List<String> FileParts { get; set; }

    public Utils()
    {
        FileParts = new List<string>();
    }

    public bool SplitFile()
    {
        bool rslt = false;
        string BaseFileName = Path.GetFileName(FileName);
        // set the size of file chunk we are going to split into  
        int BufferChunkSize = MaxFileSizeMB * (1024 * 1024);
        // set a buffer size and an array to store the buffer data as we read it  
        const int READBUFFER_SIZE = 1024;
        byte[] FSBuffer = new byte[READBUFFER_SIZE];
        // open the file to read it into chunks  
        using (FileStream FS = new FileStream(FileName, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            // calculate the number of files that will be created  
            int TotalFileParts = 0;
            if (FS.Length < BufferChunkSize)
            {
                TotalFileParts = 1;
            }
            else
            {
                float PreciseFileParts = ((float)FS.Length / (float)BufferChunkSize);
                TotalFileParts = (int)Math.Ceiling(PreciseFileParts);
            }

            int FilePartCount = 0;
            // scan through the file, and each time we get enough data to fill a chunk, write out that file  
            while (FS.Position < FS.Length)
            {
                string FilePartName = String.Format("{0}.part_{1}.{2}",
                BaseFileName, (FilePartCount + 1).ToString(), TotalFileParts.ToString());
                FilePartName = Path.Combine(TempFolder, FilePartName);
                FileParts.Add(FilePartName);
                using (FileStream FilePart = new FileStream(FilePartName, FileMode.Create))
                {
                    int bytesRemaining = BufferChunkSize;
                    int bytesRead = 0;
                    while (bytesRemaining > 0 && (bytesRead = FS.Read(FSBuffer, 0,
                     Math.Min(bytesRemaining, READBUFFER_SIZE))) > 0)
                    {
                        FilePart.Write(FSBuffer, 0, bytesRead);
                        bytesRemaining -= bytesRead;
                    }
                }
                // file written, loop for next chunk  
                FilePartCount++;
            }

        }
        return rslt;
    }

    /// <summary>
    /// original name + ".part_N.X" (N = file part number, X = total files)
    /// Objective = enumerate files in folder, look for all matching parts of split file. If found, merge and return true.
    /// </summary>
    /// <param name="FileName"></param>
    /// <returns></returns>
    public bool MergeFile(string FileName)
    {
        bool rslt = false;
        // parse out the different tokens from the filename according to the convention
        string partToken = ".part_";
        string baseFileName = FileName.Substring(0, FileName.IndexOf(partToken));
        string trailingTokens = FileName.Substring(FileName.IndexOf(partToken) + partToken.Length);
        int FileIndex = 0;
        int FileCount = 0;
        int.TryParse(trailingTokens.Substring(0, trailingTokens.IndexOf(".")), out FileIndex);
        int.TryParse(trailingTokens.Substring(trailingTokens.IndexOf(".") + 1), out FileCount);
        // get a list of all file parts in the temp folder
        string Searchpattern = Path.GetFileName(baseFileName) + partToken + "*";
        string[] FilesList = Directory.GetFiles(Path.GetDirectoryName(FileName), Searchpattern);
        //  merge .. improvement would be to confirm individual parts are there / correctly in sequence, a security check would also be important
        // only proceed if we have received all the file chunks
        if (FilesList.Count() == FileCount)
        {
            // use a singleton to stop overlapping processes
            if (!MergeFileManager.Instance.InUse(baseFileName))
            {
                MergeFileManager.Instance.AddFile(baseFileName);
                if (File.Exists(baseFileName))
                    File.Delete(baseFileName);
                // add each file located to a list so we can get them into 
                // the correct order for rebuilding the file
                List<SortedFile> MergeList = new List<SortedFile>();
                foreach (string File in FilesList)
                {
                    SortedFile sFile = new SortedFile();
                    sFile.FileName = File;
                    baseFileName = File.Substring(0, File.IndexOf(partToken));
                    trailingTokens = File.Substring(File.IndexOf(partToken) + partToken.Length);
                    int.TryParse(trailingTokens.Substring(0, trailingTokens.IndexOf(".")), out FileIndex);
                    sFile.FileOrder = FileIndex;
                    MergeList.Add(sFile);
                }
                // sort by the file-part number to ensure we merge back in the correct order
                var MergeOrder = MergeList.OrderBy(s => s.FileOrder).ToList();
                using (FileStream FS = new FileStream(baseFileName, FileMode.Create))
                {
                    // merge each file chunk back into one contiguous file stream
                    foreach (var chunk in MergeOrder)
                    {
                        try
                        {
                            using (FileStream fileChunk = new FileStream(chunk.FileName, FileMode.Open))
                            {
                                fileChunk.CopyTo(FS);
                            }
                        }
                        catch (IOException ex)
                        {
                            // handle                                
                        }
                    }
                }
                rslt = true;
                // unlock the file from singleton
            }
        }
        return rslt;
    }


}

public struct SortedFile
{
    public int FileOrder { get; set; }
    public String FileName { get; set; }
}

public class MergeFileManager
{
    private static MergeFileManager instance;
    private List<string> MergeFileList;

    private MergeFileManager()
    {
        try
        {
            MergeFileList = new List<string>();
        }
        catch { }
    }

    public static MergeFileManager Instance
    {
        get
        {
            if (instance == null)
                instance = new MergeFileManager();
            return instance;
        }
    }

    public void AddFile(string BaseFileName)
    {
        MergeFileList.Add(BaseFileName);
    }

    public bool InUse(string BaseFileName)
    {
        return MergeFileList.Contains(BaseFileName);
    }

    public bool RemoveFile(string BaseFileName)
    {
        return MergeFileList.Remove(BaseFileName);
    }
}