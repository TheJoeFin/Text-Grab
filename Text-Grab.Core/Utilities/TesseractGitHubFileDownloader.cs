using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Text_Grab.Utilities;

public sealed record TesseractDownloadResult(bool Success, string? Error)
{
    public static readonly TesseractDownloadResult Succeeded = new(true, null);
    public static TesseractDownloadResult Failed(string error) => new(false, error);
}

public class TesseractGitHubFileDownloader
{
    // It's a good practice to set a user-agent when making requests
    private const string UserAgent = "Text Grab settings language downloader";

    private readonly HttpClient _client;

    public TesseractGitHubFileDownloader()
        : this(new HttpClient())
    {
    }

    /// <param name="client">
    /// Used as-is and may be shared: the user-agent is sent per request, so the client's
    /// <see cref="HttpClient.DefaultRequestHeaders"/> are never changed.
    /// </param>
    public TesseractGitHubFileDownloader(HttpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    /// <summary>
    /// Smallest size a downloaded model may have. A truncated file or an HTML error page is far
    /// smaller than any real .traineddata and would otherwise break every OCR call.
    /// </summary>
    public long MinimumPlausibleBytes { get; init; } = 100_000;

    /// <summary>
    /// Downloads one model to <paramref name="localDestination"/>. The file is written to a
    /// <c>.tmp</c> sibling, validated, then moved into place, so a failed or cancelled download never
    /// leaves a corrupt model behind. Failures are reported in the result; cancellation throws.
    /// </summary>
    public async Task<TesseractDownloadResult> DownloadFileAsync(
        string filenameToDownload,
        string localDestination,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // Construct the URL to the raw content of the file in the GitHub repository
        // https://github.com/tesseract-ocr/tessdata
        string fileUrl = $"https://raw.githubusercontent.com/tesseract-ocr/tessdata/main/{filenameToDownload}";
        string tempPath = localDestination + ".tmp";

        try
        {
            string? folder = Path.GetDirectoryName(localDestination);
            if (!string.IsNullOrEmpty(folder))
                Directory.CreateDirectory(folder);

            using HttpRequestMessage request = new(HttpMethod.Get, fileUrl);
            request.Headers.Add("User-Agent", UserAgent);
            using HttpResponseMessage response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
                return TesseractDownloadResult.Failed($"Download failed: HTTP {(int)response.StatusCode} {response.ReasonPhrase}");

            long? totalBytes = response.Content.Headers.ContentLength;
            long written = 0;

            await using (Stream source = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (FileStream target = new(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                byte[] buffer = new byte[81920];
                int read;
                while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    written += read;
                    if (totalBytes is > 0)
                        progress?.Report((double)written / totalBytes.Value);
                }
            }

            if (written < MinimumPlausibleBytes || (totalBytes is > 0 && written != totalBytes))
                return TesseractDownloadResult.Failed($"Downloaded file is incomplete or invalid ({written} bytes).");

            File.Move(tempPath, localDestination, overwrite: true);
            progress?.Report(1);
            return TesseractDownloadResult.Succeeded;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException)
        {
            return TesseractDownloadResult.Failed(ex.Message);
        }
        finally
        {
            try { File.Delete(tempPath); }
            catch (Exception) { /* best effort cleanup */ }
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
