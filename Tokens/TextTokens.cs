using System;
using System.IO;
using System.Linq;
using System.Web;
using HtmlAgilityPack;
using Orchard;
using Orchard.Localization;
using Orchard.Tokens;

namespace LimitImages.Tokens
{
    public class TextTokens : ITokenProvider
    {
        private readonly IWorkContextAccessor _workContext;

        public TextTokens(IWorkContextAccessor workContext) {
            _workContext = workContext;
            T = NullLocalizer.Instance;
        }

        public Localizer T { get; set; }

        public void Describe(DescribeContext context) {
            context.For("Text", T("Text"), T("Ananda Tokens for text strings"))
                .Token("LimitImages:*", T("LimitImages:<width|number>"), T("Limits images to the given maximum width"))
                ;
        }

        public void Evaluate(EvaluateContext context) {
            context.For("Text", () => "")
                .Token(token => {
                    if (token.StartsWith("LimitImages:", StringComparison.OrdinalIgnoreCase)) {
                        var param = token.Substring("LimitImages:".Length);
                        return param;
                    }
                    return null;
                }, (token, t) => LimitImages(t, token));
        }

        private string LimitImages(string text, string param) {
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(text);
            int widthLimit;
            // set a default if width not given
            if (!int.TryParse(param, out widthLimit)) {
                widthLimit = 560;
            }
            foreach (var img in htmlDoc.DocumentNode.Descendants("img").Where(i => i.Attributes["src"].Value.StartsWith(_workContext.GetContext().CurrentSite.BaseUrl))) {
                var srcAttribute = img.Attributes["src"];
                if (srcAttribute != null) {
                    var src = HttpUtility.HtmlDecode(srcAttribute.Value);
                    var startOfQueryString = src.IndexOf("?", StringComparison.Ordinal);
                    if (startOfQueryString >= 0) {
                        // there's a query string, see if it's imageresizer.net
                        var parsedQueryString = HttpUtility.ParseQueryString(src.Substring(startOfQueryString));
                        var widthValue = parsedQueryString["w"] ?? parsedQueryString["width"];
                        var heightValue = parsedQueryString["h"] ?? parsedQueryString["height"];
                        if (widthValue != null) {
                            int width;
                            if (int.TryParse(widthValue, out width) && width > widthLimit) { 
                                var newWidthParam = string.Format("width={0}", widthLimit);
                                src = src.Replace(string.Format("width={0}", widthValue), newWidthParam).Replace(string.Format("w={0}", widthValue), newWidthParam);
                                if (heightValue != null) {
                                    src = src.Replace(string.Format("&height={0}", heightValue), "").Replace(string.Format("&h={0}", heightValue), "");
                                }
                                RemoveWidthHeightAttributes(img);
                            }
                        }
                    }
                    else {
                        // no query string (so no imageresizer), try the width attribute
                        var widthAttribute = img.Attributes["width"];
                        if (widthAttribute != null) {
                            int width;
                            if (int.TryParse(widthAttribute.Value, out width) && width > widthLimit) {
                                src += string.Format("?width={0}", widthLimit);
                                RemoveWidthHeightAttributes(img);
                            }
                        }
                    }
                    img.Attributes["src"].Value = src;
                }
            }
            string result;
            using (var writer = new StringWriter())
            {
                htmlDoc.Save(writer);
                result = writer.ToString();
            }
            return result;
        }

        private static void RemoveWidthHeightAttributes(HtmlNode img) {
            img.Attributes["width"].Remove();
            img.Attributes["height"].Remove();
        }
    }
}