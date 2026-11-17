using Markdig;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Deosrc.Tammi.UI.Updates;

/// <summary>
/// Turns a release's notes, which are markdown written on GitHub, into HTML to show in the app.
/// </summary>
public static class ReleaseNotesHtml
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    /// <summary>
    /// Converts markdown to HTML. Links open in the browser, not in the app's window.
    /// </summary>
    public static string ToHtml(string markdown)
    {
        var document = Markdown.Parse(markdown, Pipeline);
        TameHtml(document);
        foreach (var link in document.Descendants<LinkInline>().Where(link => !link.IsImage))
        {
            var attributes = link.GetAttributes();
            attributes.AddPropertyIfNotExist("target", "_blank");
            attributes.AddPropertyIfNotExist("rel", "noreferrer");
        }

        return document.ToHtml(Pipeline);
    }

    // The notes' raw HTML isn't passed on, so a release can't put markup in the app. Comments, which release notes often
    // have (e.g. left in from a template), are dropped, and any other HTML is shown as text.
    private static void TameHtml(MarkdownDocument document)
    {
        foreach (var block in document.Descendants<HtmlBlock>().ToList())
        {
            if (block.Parent is not ContainerBlock parent)
            {
                continue;
            }

            var text = block.Lines.ToString();
            if (block.Type != HtmlBlockType.Comment && !IsComment(text))
            {
                var paragraph = new ParagraphBlock { Inline = new ContainerInline() };
                paragraph.Inline.AppendChild(new LiteralInline(text));
                parent.Insert(parent.IndexOf(block), paragraph);
            }

            parent.Remove(block);
        }

        foreach (var inline in document.Descendants<HtmlInline>().ToList())
        {
            if (!IsComment(inline.Tag))
            {
                inline.ReplaceBy(new LiteralInline(inline.Tag));
            }
            else
            {
                inline.Remove();
            }
        }
    }

    private static bool IsComment(string html) => html.TrimStart().StartsWith("<!--", StringComparison.Ordinal);
}
