using System.Net;
using System.Text;

namespace EasyAI.Web
{
    public interface IHtmlPageRenderer
    {
        string Render(string prompt, string reply);
    }

    public sealed class HtmlPageRenderer : IHtmlPageRenderer
    {
        public string Render(string prompt, string reply)
        {
            var sb = new StringBuilder();
            sb.Append(PageStart());
            sb.Append(FormAndLiveGrid(prompt, reply));
            sb.Append(PageEnd());
            return sb.ToString();
        }

        private static string PageStart() => @"
<!doctype html>
<html lang=""pl"">
<head>
<meta charset=""utf-8"">
<meta name=""viewport"" content=""width=device-width,initial-scale=1"">
<title>EasyAI</title>
<style>
  :root { --bg:#0f172a; --panel:#111827; --ink:#e5e7eb; --muted:#9ca3af; --accent:#38bdf8; --border:#1f2937; }
  * { box-sizing:border-box }
  body { margin:0; font-family: system-ui,-apple-system,Segoe UI,Roboto,Arial,sans-serif; background:var(--bg); color:var(--ink); }
  .wrap { max-width:1000px; margin:0 auto; padding:32px 16px; }
  h1 { margin:0 0 6px; font-size:28px; font-weight:700; }
  .muted { color:var(--muted); font-size:14px; margin:0 0 16px; }
  .card { background:var(--panel); border:1px solid var(--border); border-radius:14px; padding:16px; }
  textarea { width:100%; min-height:160px; resize:vertical; padding:12px; border-radius:10px; border:1px solid var(--border); background:#0b1220; color:var(--ink);
             font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace; }
  .row { display:flex; gap:10px; align-items:center; margin-top:12px; flex-wrap:wrap; }
  button { border:0; padding:10px 14px; border-radius:10px; background:var(--accent); color:#001018; font-weight:600; cursor:pointer; }
  button[disabled] { opacity:.6; cursor:default; }
  .btn-secondary { background:#374151; color:#e5e7eb; }
  select, .select {
    background:#0b1220; color:var(--ink); border:1px solid var(--border); border-radius:10px; padding:8px 10px; min-width:280px;
  }
  .grid { display:grid; grid-template-columns:1fr; gap:12px; margin-top:16px; }
  @media (min-width:900px){ .grid { grid-template-columns:1fr 1fr; } }
  .blk { background:#0b1220; border:1px solid var(--border); border-radius:10px; padding:10px; }
  .blk h3 { margin:0 0 8px; font-size:13px; color:var(--muted); font-weight:600; }
  pre { white-space:pre-wrap; word-break:break-word; margin:0; font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace; font-size:13px; }
  footer { margin-top:18px; color:var(--muted); font-size:12px; }
  .status { font-size:12px; color:var(--muted); }
  .error { color:#fecaca; }
  .ok { color:#86efac; }
  a.link { color:var(--muted); text-decoration:underline; }
</style>
</head>
<body>
  <div class=""wrap"">
    <h1>EasyAI</h1>
    <p class=""muted"">Wybierz model z <code>/models</code>, wyślij prompt i oglądaj strumień odpowiedzi w czasie rzeczywistym.</p>
    <div class=""card"">";

        private static string FormAndLiveGrid(string prompt, string reply)
        {
            string H(string s) => WebUtility.HtmlEncode(s ?? string.Empty);

            var form = @"
      <form id=""promptForm"" action=""/prompt"" method=""post"">
        <div class=""row"">
          <label class=""muted"" for=""modelSelect"">Model:</label>
          <select id=""modelSelect"" class=""select""></select>
          <button id=""loadModel"" type=""button"" class=""btn-secondary"">Załaduj model</button>
          <span id=""modelStatus"" class=""status""></span>
        </div>

        <label for=""prompt"" class=""muted"">Prompt</label>
        <textarea id=""prompt"" name=""prompt"" placeholder=""Wpisz prompt..."">" + H(prompt) + @"</textarea>
        <div class=""row"">
          <button id=""sendLive"" type=""button"">Wyślij (na żywo)</button>
          <button id=""stopLive"" type=""button"" class=""btn-secondary"">Zatrzymaj</button>
          <button id=""sendClassic"" type=""submit"" class=""btn-secondary"">Klasyczny submit</button>
          <a href=""/"" class=""link"">Wyczyść</a>
          <span id=""status"" class=""status""></span>
        </div>
      </form>";

            var grid = @"
      <div class=""grid"">
        <div class=""blk"">
          <h3>Prompt</h3>
          <pre id=""promptBox"">" + H(prompt) + @"</pre>
        </div>
        <div class=""blk"">
          <h3>Odpowiedź (na żywo)</h3>
          <pre id=""replyBox"">" + H(reply) + @"</pre>
        </div>
      </div>";

            var script = @"
      <script>
      (function () {
        const modelSelect = document.getElementById('modelSelect');
        const loadModelBtn = document.getElementById('loadModel');
        const modelStatus = document.getElementById('modelStatus');

        const promptInput = document.getElementById('prompt');
        const promptBox = document.getElementById('promptBox');
        const replyBox = document.getElementById('replyBox');
        const sendLive = document.getElementById('sendLive');
        const stopLive = document.getElementById('stopLive');
        const sendClassic = document.getElementById('sendClassic');
        const statusEl = document.getElementById('status');

        let controller = null;

        function setBusy(b, msg) {
          sendLive.disabled = b;
          sendClassic.disabled = b;
          stopLive.disabled = !b;
          statusEl.textContent = msg || (b ? 'Strumieniowanie...' : '');
        }

        function setModelBusy(b, msg) {
          loadModelBtn.disabled = b;
          modelSelect.disabled = b;
          modelStatus.textContent = msg || (b ? 'Ładowanie modelu…' : '');
          modelStatus.classList.toggle('ok', !b && !!msg && msg.includes('Załadowano'));
          if (!b) {
            modelStatus.classList.toggle('error', !!msg && msg.startsWith('Błąd'));
          }
        }

        function appendText(text) {
          // usuń trailing 'User:' gdy model czeka na kolejny prompt
          let clean = text;
          if (clean.endsWith('User:')) clean = clean.slice(0, -5);
          replyBox.textContent += clean;
          replyBox.scrollTop = replyBox.scrollHeight;
        }

        async function refreshModels() {
          try {
            const [listRes, currRes] = await Promise.all([
              fetch('/api/models'),
              fetch('/api/current-model')
            ]);
            const listJson = await listRes.json();
            const currJson = await currRes.json();

            const models = Array.isArray(listJson.models) ? listJson.models : [];
            const current = (currJson && currJson.name) ? currJson.name : '';

            modelSelect.innerHTML = '';
            for (const m of models) {
              const opt = document.createElement('option');
              opt.value = m; opt.textContent = m;
              if (m === current) opt.selected = true;
              modelSelect.appendChild(opt);
            }
            modelStatus.textContent = current ? ('Aktualny: ' + current) : 'Brak załadowanego modelu';
          } catch {
            modelStatus.textContent = 'Błąd pobierania listy modeli';
            modelStatus.classList.add('error');
          }
        }

        loadModelBtn.addEventListener('click', async () => {
          const name = modelSelect.value;
          if (!name) return;
          setModelBusy(true);
          try {
            const res = await fetch('/api/select-model', {
              method: 'POST',
              headers: { 'Content-Type': 'application/json' },
              body: JSON.stringify({ name })
            });
            if (!res.ok) {
              const err = await res.text();
              throw new Error(err || ('HTTP ' + res.status));
            }
            setModelBusy(false, 'Załadowano: ' + name);
          } catch (e) {
            setModelBusy(false, 'Błąd: ' + (e && e.message ? e.message : e));
          } finally {
            await refreshModels();
          }
        });

        stopLive.addEventListener('click', () => {
          if (controller) {
            controller.abort();
            controller = null;
            setBusy(false, 'Zatrzymano.');
          }
        });

        sendLive.addEventListener('click', async () => {
          const text = (promptInput.value || '').trim();
          if (!text) return;

          promptBox.textContent = text;
          replyBox.textContent = '';
          statusEl.classList.remove('error');

          controller = new AbortController();
          setBusy(true);

          try {
            const res = await fetch('/api/stream', {
              method: 'POST',
              headers: { 'Content-Type': 'application/json' },
              body: JSON.stringify({ prompt: text }),
              signal: controller.signal
            });
            if (!res.ok || !res.body) throw new Error('HTTP ' + res.status);

            const reader = res.body.getReader();
            const decoder = new TextDecoder();
            let buffer = '';

            while (true) {
              const { done, value } = await reader.read();
              if (done) break;
              buffer += decoder.decode(value, { stream: true });

              // każdy event SSE kończy się podwójną nową linią
              let idx;
              while ((idx = buffer.indexOf('\n\n')) >= 0) {
                const rawEvent = buffer.slice(0, idx);
                buffer = buffer.slice(idx + 2);

                // złóż wszystkie linie data: w jeden payload (zachowaj \n pomiędzy)
                const lines = rawEvent.split('\n')
                  .filter(l => l.startsWith('data: '))
                  .map(l => l.slice(6));
                const payload = lines.join('\n');

                if (payload === '[DONE]') {
                  setBusy(false, 'Zakończono.');
                  return;
                }
                appendText(payload);
              }
            }
            setBusy(false, 'Zakończono.');
          } catch (err) {
            if (controller && controller.signal.aborted) return;
            statusEl.classList.add('error');
            statusEl.textContent = 'Błąd: ' + (err && err.message ? err.message : err);
            setBusy(false);
          } finally {
            controller = null;
          }
        });

        // init
        refreshModels();
      })();
      </script>";

            return form + grid + script;
        }

        private static string PageEnd() => @"
      <footer>
        Endpointy: <code>GET /api/models</code>, <code>POST /api/select-model</code>, 
        <code>GET /api/current-model</code>, <code>POST /api/stream</code>, <code>POST /prompt</code>, <code>GET /health</code>
      </footer>
    </div>
  </div>
</body>
</html>";
    }
}
