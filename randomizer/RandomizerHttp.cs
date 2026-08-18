using System;
using System.Collections.Specialized;
using System.Text;

// Managed face of the sidecar's async HTTP, which unlike System.Net can speak
// modern TLS and so reach the canonical https host.
//
// Requests never block: start one, poll Done, then read Status/Body. Every
// request must reach Done or be Abandoned, or its response is held until exit.
public class RandomizerHttp {
    public static bool Available => NativeWebSocket.AsyncHttpAvailable;

    public static RandomizerHttp Get(string url) {
        return Start("GET", url, null, null);
    }

    public static RandomizerHttp PostForm(string url, NameValueCollection values) {
        return Start("POST", url, Encode(values), "application/x-www-form-urlencoded");
    }

    private static RandomizerHttp Start(string method, string url, string body, string contentType) {
        try {
            var handle = NativeWebSocket.HttpBegin(method, url, body, contentType);
            return handle == 0 ? null : new RandomizerHttp(handle, url);
        } catch (Exception e) {
            Randomizer.log($"http: could not start {method} {url}: {e.Message}");
            return null;
        }
    }

    private RandomizerHttp(int handle, string url) {
        m_handle = handle;
        Url = url;
    }

    public string Url { get; private set; }

    public int Status { get; private set; }

    public string Body { get; private set; }

    public string Error { get; private set; }

    public bool Ok => Status >= 200 && Status < 300;

    public bool Done {
        get {
            if (m_done) {
                return true;
            }

            var status = NativeWebSocket.HttpStatus(m_handle);
            if (status == NativeWebSocket.HttpPending) {
                return false;
            }

            Status = status;
            Body = NativeWebSocket.HttpResponse(m_handle);
            Error = NativeWebSocket.HttpResponseError(m_handle);
            NativeWebSocket.HttpRelease(m_handle);
            m_done = true;
            return true;
        }
    }

    // stop caring about a request without waiting for it
    public void Abandon() {
        if (m_done) {
            return;
        }

        NativeWebSocket.HttpRelease(m_handle);
        m_done = true;
    }

    private static string Encode(NameValueCollection values) {
        var sb = new StringBuilder();
        foreach (string key in values) {
            if (sb.Length > 0) {
                sb.Append('&');
            }

            sb.Append(Uri.EscapeDataString(key)).Append('=').Append(EscapeLong(values[key]));
        }

        return sb.ToString();
    }

    // Uri.EscapeDataString throws past ~32k chars and seeds get there; chunking
    // is safe because seed text is ASCII.
    public static string EscapeLong(string s) {
        var sb = new StringBuilder();
        for (var i = 0; i < s.Length; i += 16000) {
            sb.Append(Uri.EscapeDataString(s.Substring(i, Math.Min(16000, s.Length - i))));
        }

        return sb.ToString();
    }

    private readonly int m_handle;

    private bool m_done;
}
