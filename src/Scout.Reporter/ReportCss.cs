namespace Scout.Reporter;

/// <summary>
/// The report's entire stylesheet, embedded verbatim into the single output HTML file. No
/// external stylesheet, font, icon set, or CDN is ever referenced — the report must open
/// correctly from a local file, an email attachment, or a shared folder with no network access.
/// </summary>
internal static class ReportCss
{
    public const string Content = """
        :root {
          color-scheme: light;
          --bg: #f4f4f7;
          --card-bg: #ffffff;
          --text: #1d1d1f;
          --muted: #6b6b70;
          --border: #e6e6ea;
          --good-bg: #e8f5e9; --good-fg: #1b5e20;
          --warn-bg: #fff8e1; --warn-fg: #7a5c00;
          --attention-bg: #fff3e0; --attention-fg: #8a4b00;
          --bad-bg: #fdecea; --bad-fg: #8e1c14;
          --unknown-bg: #eeeeee; --unknown-fg: #424242;
        }

        * { box-sizing: border-box; }

        body {
          margin: 0;
          background: var(--bg);
          color: var(--text);
          font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif;
          line-height: 1.5;
        }

        .verdict {
          padding: 2.5rem 1.5rem;
          text-align: center;
        }

        .verdict-headline {
          margin: 0 0 .5rem;
          font-size: clamp(1.5rem, 4vw, 2.25rem);
          font-weight: 700;
        }

        .verdict-reason {
          margin: 0;
          font-size: 1.05rem;
          opacity: .85;
        }

        .verdict-good { background: var(--good-bg); color: var(--good-fg); }
        .verdict-warn { background: var(--warn-bg); color: var(--warn-fg); }
        .verdict-attention { background: var(--attention-bg); color: var(--attention-fg); }
        .verdict-bad { background: var(--bad-bg); color: var(--bad-fg); }
        .verdict-unknown { background: var(--unknown-bg); color: var(--unknown-fg); }

        .container {
          max-width: 880px;
          margin: 0 auto;
          padding: 1.5rem;
        }

        .card {
          background: var(--card-bg);
          border: 1px solid var(--border);
          border-radius: 12px;
          padding: 1.25rem 1.5rem;
          margin-bottom: 1.25rem;
        }

        .card h2 {
          margin-top: 0;
          font-size: 1.15rem;
        }

        table { width: 100%; border-collapse: collapse; }

        th, td {
          text-align: left;
          padding: .55rem .6rem;
          border-bottom: 1px solid var(--border);
          font-size: .92rem;
          vertical-align: top;
        }

        thead th { color: var(--muted); font-weight: 600; font-size: .8rem; text-transform: uppercase; letter-spacing: .02em; }

        table.kv th { width: 11rem; color: var(--muted); font-weight: 600; border-bottom: 1px solid var(--border); }

        tbody tr:last-child td, tbody tr:last-child th { border-bottom: none; }

        code {
          font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
          font-size: .85em;
          background: var(--bg);
          padding: .1em .35em;
          border-radius: 4px;
        }

        .badge {
          display: inline-block;
          padding: .2em .6em;
          border-radius: 999px;
          font-size: .8rem;
          font-weight: 600;
          white-space: nowrap;
        }

        .badge-good { background: var(--good-bg); color: var(--good-fg); }
        .badge-warn { background: var(--warn-bg); color: var(--warn-fg); }
        .badge-attention { background: var(--attention-bg); color: var(--attention-fg); }
        .badge-bad { background: var(--bad-bg); color: var(--bad-fg); }
        .badge-unknown { background: var(--unknown-bg); color: var(--unknown-fg); }

        details summary {
          cursor: pointer;
          font-weight: 600;
          font-size: 1.15rem;
          padding: .1rem 0;
        }

        details[open] summary { margin-bottom: .75rem; }

        .muted { color: var(--muted); font-size: .88rem; }

        .distro-name { font-size: 1.15rem; font-weight: 700; margin: 0 0 .4rem; }

        ul, ol { padding-left: 1.25rem; margin: .5rem 0; }
        li { margin: .35rem 0; }

        a { color: #0b57d0; }
        a:visited { color: #0b57d0; }

        footer {
          text-align: center;
          color: var(--muted);
          font-size: .82rem;
          padding: 1rem 1.5rem 2rem;
        }
        """;
}
