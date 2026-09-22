using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Text_Grab.Utilities;

public class TesseractGitHubFileDownloader
{
    private readonly HttpClient _client;

    public TesseractGitHubFileDownloader()
    {
        _client = new HttpClient();
        // It's a good practice to set a user-agent when making requests
        _client.DefaultRequestHeaders.Add("User-Agent", "Text Grab settings language downloader");
    }

    public async Task DownloadFileAsync(string filenameToDownload, string localDestination)
    {
        // Construct the URL to the raw content of the file in the GitHub repository
        // https://github.com/tesseract-ocr/tessdata
        string fileUrl = $"https://raw.githubusercontent.com/tesseract-ocr/tessdata/main/{filenameToDownload}";

        try
        {
            // Send a GET request to the specified URL
            HttpResponseMessage response = await _client.GetAsync(fileUrl);
            response.EnsureSuccessStatusCode();

            // Read the response content
            byte[] fileContents = await response.Content.ReadAsByteArrayAsync();

            // Write the content to a file on the local file system
            await File.WriteAllBytesAsync(localDestination, fileContents);
            Console.WriteLine("File downloaded successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }

    public static readonly string[] tesseractTrainedDataFileNames = [
        "afr.traineddata",
        "amh.traineddata",
        "ara.traineddata",
        "asm.traineddata",
        "aze.traineddata",
        "aze_cyrl.traineddata",
        "bel.traineddata",
        "ben.traineddata",
        "bod.traineddata",
        "bos.traineddata",
        "bre.traineddata",
        "bul.traineddata",
        "cat.traineddata",
        "ceb.traineddata",
        "ces.traineddata",
        "chi_sim.traineddata",
        "chi_sim_vert.traineddata",
        "chi_tra.traineddata",
        "chi_tra_vert.traineddata",
        "chr.traineddata",
        "cos.traineddata",
        "cym.traineddata",
        "dan.traineddata",
        "dan_frak.traineddata",
        "deu.traineddata",
        "deu_frak.traineddata",
        "div.traineddata",
        "dzo.traineddata",
        "ell.traineddata",
        "eng.traineddata",
        "enm.traineddata",
        "epo.traineddata",
        "equ.traineddata",
        "est.traineddata",
        "eus.traineddata",
        "fao.traineddata",
        "fas.traineddata",
        "fil.traineddata",
        "fin.traineddata",
        "fra.traineddata",
        "frk.traineddata",
        "frm.traineddata",
        "fry.traineddata",
        "gla.traineddata",
        "gle.traineddata",
        "glg.traineddata",
        "grc.traineddata",
        "guj.traineddata",
        "hat.traineddata",
        "heb.traineddata",
        "hin.traineddata",
        "hrv.traineddata",
        "hun.traineddata",
        "hye.traineddata",
        "iku.traineddata",
        "ind.traineddata",
        "isl.traineddata",
        "ita.traineddata",
        "ita_old.traineddata",
        "jav.traineddata",
        "jpn.traineddata",
        "jpn_vert.traineddata",
        "kan.traineddata",
        "kat.traineddata",
        "kat_old.traineddata",
        "kaz.traineddata",
        "khm.traineddata",
        "kir.traineddata",
        "kmr.traineddata",
        "kor.traineddata",
        "kor_vert.traineddata",
        "lao.traineddata",
        "lat.traineddata",
        "lav.traineddata",
        "lit.traineddata",
        "ltz.traineddata",
        "mal.traineddata",
        "mar.traineddata",
        "mkd.traineddata",
        "mlt.traineddata",
        "mon.traineddata",
        "mri.traineddata",
        "msa.traineddata",
        "mya.traineddata",
        "nep.traineddata",
        "nld.traineddata",
        "nor.traineddata",
        "oci.traineddata",
        "ori.traineddata",
        "osd.traineddata",
        "pan.traineddata",
        "pol.traineddata",
        "por.traineddata",
        "pus.traineddata",
        "que.traineddata",
        "ron.traineddata",
        "rus.traineddata",
        "san.traineddata",
        "sin.traineddata",
        "slk.traineddata",
        "slk_frak.traineddata",
        "slv.traineddata",
        "snd.traineddata",
        "spa.traineddata",
        "spa_old.traineddata",
        "sqi.traineddata",
        "srp.traineddata",
        "srp_latn.traineddata",
        "sun.traineddata",
        "swa.traineddata",
        "swe.traineddata",
        "syr.traineddata",
        "tam.traineddata",
        "tat.traineddata",
        "tel.traineddata",
        "tgk.traineddata",
        "tgl.traineddata",
        "tha.traineddata",
        "tir.traineddata",
        "ton.traineddata",
        "tur.traineddata",
        "uig.traineddata",
        "ukr.traineddata",
        "urd.traineddata",
        "uzb.traineddata",
        "uzb_cyrl.traineddata",
        "vie.traineddata",
        "yid.traineddata",
        "yor.traineddata",
    ];
}
