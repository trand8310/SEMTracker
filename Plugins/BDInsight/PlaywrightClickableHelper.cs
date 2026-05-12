using Microsoft.Playwright;
using System.Globalization;
using System.Text.Json;


namespace BDInsight
{

 
        public sealed class ClickAreaOptions
        {
            /// <summary>X 最小百分比，0~1</summary>
            public double MinXPercent { get; set; } = 0.15;

            /// <summary>X 最大百分比，0~1</summary>
            public double MaxXPercent { get; set; } = 0.80;

            /// <summary>Y 最小百分比，0~1</summary>
            public double MinYPercent { get; set; } = 0.20;

            /// <summary>Y 最大百分比，0~1</summary>
            public double MaxYPercent { get; set; } = 0.75;

            /// <summary>是否硬过滤优先区域外元素。false=区域外也保留，只是降分</summary>
            public bool StrictPreferredArea { get; set; } = false;

            /// <summary>最多返回多少个候选</summary>
            public int MaxCount { get; set; } = 100;

            /// <summary>最小宽度</summary>
            public double MinWidth { get; set; } = 3;

            /// <summary>最小高度</summary>
            public double MinHeight { get; set; } = 3;

            /// <summary>文本最大截断长度</summary>
            public int MaxTextLength { get; set; } = 100;

            /// <summary>是否偏好常见操作词</summary>
            public bool PreferActionText { get; set; } = true;
        }

        public sealed class ClickableNodeInfo
        {
            public string FrameUrl { get; set; } = "";
            public string TagName { get; set; } = "";
            public string Text { get; set; } = "";
            public string Selector { get; set; } = "";
            public string XPath { get; set; } = "";

            public double Left { get; set; }
            public double Top { get; set; }
            public double Width { get; set; }
            public double Height { get; set; }

            public double CenterX { get; set; }
            public double CenterY { get; set; }

            public int Score { get; set; }

            public bool IsVisible { get; set; }
            public bool IsTopMost { get; set; }
            public bool InViewport { get; set; }
            public bool InPreferredArea { get; set; }
            public bool Enabled { get; set; }
            public bool Editable { get; set; }

            public override string ToString()
            {
                return $"{Score} | {TagName} | {Text} | {Selector}";
            }
        }

        public static class PlaywrightClickableHelper
        {
            public static async Task<List<ClickableNodeInfo>> GetClickableNodesAsync(
                IPage page,
                ClickAreaOptions? options = null,
                CancellationToken cancellationToken = default)
            {
                options ??= new ClickAreaOptions();

                var result = new List<ClickableNodeInfo>();

                if (page == null || page.IsClosed)
                    return result;

                foreach (var frame in page.Frames)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var items = await GetClickableNodesFromFrameAsync(frame, options);
                        if (items.Count > 0)
                            result.AddRange(items);
                    }
                    catch
                    {
                        // 某些 frame 可能不可访问，忽略
                    }
                }

                return result
                    .OrderByDescending(x => x.Score)
                    .ThenByDescending(x => x.InPreferredArea)
                    .ThenByDescending(x => x.Width * x.Height)
                    .ToList();
            }

            public static async Task<ClickableNodeInfo?> GetBestClickableNodeAsync(
                IPage page,
                ClickAreaOptions? options = null,
                CancellationToken cancellationToken = default)
            {
                var nodes = await GetClickableNodesAsync(page, options, cancellationToken);
                return nodes.FirstOrDefault();
            }

            public static async Task<(IFrame Frame, ILocator Locator, ClickableNodeInfo Node)?> GetBestClickableLocatorAsync(
                IPage page,
                ClickAreaOptions? options = null,
                CancellationToken cancellationToken = default)
            {
                options ??= new ClickAreaOptions();

                var nodes = await GetClickableNodesAsync(page, options, cancellationToken);
                if (nodes.Count == 0)
                    return null;

                foreach (var node in nodes)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var frame = FindFrameByUrl(page, node.FrameUrl);
                    if (frame == null)
                        continue;

                    if (string.IsNullOrWhiteSpace(node.Selector))
                        continue;

                    try
                    {
                        var locator = frame.Locator(node.Selector).First;
                        if (await locator.CountAsync() == 0)
                            continue;

                        return (frame, locator, node);
                    }
                    catch
                    {
                    }
                }

                return null;
            }

            public static async Task<bool> ClickBestNodeAsync(
                IPage page,
                ClickAreaOptions? options = null,
                int topN = 10,
                CancellationToken cancellationToken = default)
            {
                options ??= new ClickAreaOptions();

                var nodes = await GetClickableNodesAsync(page, options, cancellationToken);
                if (nodes.Count == 0)
                    return false;

                foreach (var node in nodes.Take(Math.Max(1, topN)))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var frame = FindFrameByUrl(page, node.FrameUrl);
                    if (frame == null)
                        continue;

                    var ok = await ClickNodeWithFallbackAsync(page, frame, node, cancellationToken);
                    if (ok)
                        return true;
                }

                return false;
            }

            public static async Task<bool> ClickNodeWithFallbackAsync(
                IPage page,
                IFrame frame,
                ClickableNodeInfo node,
                CancellationToken cancellationToken = default)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(node.Selector))
                        return false;

                    var locator = frame.Locator(node.Selector).First;

                    if (await locator.CountAsync() == 0)
                        return false;

                    try
                    {
                        await locator.ScrollIntoViewIfNeededAsync();
                    }
                    catch
                    {
                    }

                    try
                    {
                        await locator.ClickAsync(new LocatorClickOptions
                        {
                            Timeout = 2500,
                            Force = false
                        });

                        return true;
                    }
                    catch
                    {
                    }

                    try
                    {
                        await page.Mouse.ClickAsync((float)node.CenterX, (float)node.CenterY);
                        return true;
                    }
                    catch
                    {
                    }

                    try
                    {
                        await locator.EvaluateAsync("el => el.click()");
                        return true;
                    }
                    catch
                    {
                    }

                    return false;
                }
                catch
                {
                    return false;
                }
            }

            private static IFrame? FindFrameByUrl(IPage page, string frameUrl)
            {
                if (string.IsNullOrWhiteSpace(frameUrl))
                    return null;

                return page.Frames.FirstOrDefault(f =>
                    string.Equals(f.Url, frameUrl, StringComparison.OrdinalIgnoreCase));
            }

            private static async Task<List<ClickableNodeInfo>> GetClickableNodesFromFrameAsync(
                IFrame frame,
                ClickAreaOptions options)
            {
                var js = BuildCollectScript(options);

                var json = await frame.EvaluateAsync<string>(js);
                if (string.IsNullOrWhiteSpace(json))
                    return new List<ClickableNodeInfo>();

                var items = JsonSerializer.Deserialize<List<ClickableNodeInfo>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return items ?? new List<ClickableNodeInfo>();
            }

            private static string BuildCollectScript(ClickAreaOptions options)
            {
                string minX = options.MinXPercent.ToString(CultureInfo.InvariantCulture);
                string maxX = options.MaxXPercent.ToString(CultureInfo.InvariantCulture);
                string minY = options.MinYPercent.ToString(CultureInfo.InvariantCulture);
                string maxY = options.MaxYPercent.ToString(CultureInfo.InvariantCulture);
                string strictPreferredArea = options.StrictPreferredArea ? "true" : "false";
                string maxCount = options.MaxCount.ToString(CultureInfo.InvariantCulture);
                string minWidth = options.MinWidth.ToString(CultureInfo.InvariantCulture);
                string minHeight = options.MinHeight.ToString(CultureInfo.InvariantCulture);
                string maxTextLength = options.MaxTextLength.ToString(CultureInfo.InvariantCulture);
                string preferActionText = options.PreferActionText ? "true" : "false";

                return $$"""
() => {
    const CONFIG = {
        minXPercent: {{minX}},
        maxXPercent: {{maxX}},
        minYPercent: {{minY}},
        maxYPercent: {{maxY}},
        strictPreferredArea: {{strictPreferredArea}},
        maxCount: {{maxCount}},
        minWidth: {{minWidth}},
        minHeight: {{minHeight}},
        maxTextLength: {{maxTextLength}},
        preferActionText: {{preferActionText}}
    };

    function cssEscape(value) {
        if (window.CSS && CSS.escape) return CSS.escape(value);
        return String(value).replace(/([ !"#$%&'()*+,./:;<=>?@[\\\]^`{|}~])/g, '\\$1');
    }

    function normalizeText(text) {
        return (text || '').replace(/\s+/g, ' ').trim().slice(0, CONFIG.maxTextLength);
    }

    function buildCssSelector(el) {
        if (!(el instanceof Element)) return '';
        const parts = [];
        let cur = el;

        while (cur && cur.nodeType === 1 && parts.length < 8) {
            let part = cur.tagName.toLowerCase();

            if (cur.id) {
                part += '#' + cssEscape(cur.id);
                parts.unshift(part);
                break;
            }

            const cls = [...cur.classList]
                .filter(Boolean)
                .slice(0, 2)
                .map(cssEscape);

            if (cls.length) {
                part += '.' + cls.join('.');
            }

            const parent = cur.parentElement;
            if (parent) {
                const siblings = [...parent.children].filter(x => x.tagName === cur.tagName);
                if (siblings.length > 1) {
                    const index = siblings.indexOf(cur) + 1;
                    part += `:nth-of-type(${index})`;
                }
            }

            parts.unshift(part);
            cur = cur.parentElement;
        }

        return parts.join(' > ');
    }

    function buildXPath(el) {
        if (!(el instanceof Element)) return '';
        const parts = [];
        let cur = el;

        while (cur && cur.nodeType === 1) {
            let index = 1;
            let sib = cur.previousElementSibling;
            while (sib) {
                if (sib.tagName === cur.tagName) index++;
                sib = sib.previousElementSibling;
            }
            parts.unshift(`${cur.tagName.toLowerCase()}[${index}]`);
            cur = cur.parentElement;
        }

        return '/' + parts.join('/');
    }

    function getAreaBounds() {
        const minX = window.innerWidth * CONFIG.minXPercent;
        const maxX = window.innerWidth * CONFIG.maxXPercent;
        const minY = window.innerHeight * CONFIG.minYPercent;
        const maxY = window.innerHeight * CONFIG.maxYPercent;

        return { minX, maxX, minY, maxY };
    }

    function getRectCenter(rect) {
        return {
            cx: rect.left + rect.width / 2,
            cy: rect.top + rect.height / 2
        };
    }

    function isInViewport(rect) {
        return rect.width >= CONFIG.minWidth &&
               rect.height >= CONFIG.minHeight &&
               rect.bottom > 0 &&
               rect.right > 0 &&
               rect.top < window.innerHeight &&
               rect.left < window.innerWidth;
    }

    function isInPreferredArea(rect) {
        const { minX, maxX, minY, maxY } = getAreaBounds();
        const { cx, cy } = getRectCenter(rect);

        return cx >= minX && cx <= maxX && cy >= minY && cy <= maxY;
    }

    function calcAreaBonus(rect) {
        const { minX, maxX, minY, maxY } = getAreaBounds();
        const { cx, cy } = getRectCenter(rect);

        const areaCenterX = (minX + maxX) / 2;
        const areaCenterY = (minY + maxY) / 2;

        const dx = cx - areaCenterX;
        const dy = cy - areaCenterY;

        const halfW = Math.max(1, (maxX - minX) / 2);
        const halfH = Math.max(1, (maxY - minY) / 2);

        const nx = dx / halfW;
        const ny = dy / halfH;

        const dist = Math.sqrt(nx * nx + ny * ny);

        if (!isInPreferredArea(rect)) {
            return -20 - Math.min(20, Math.round(dist * 10));
        }

        return Math.max(0, Math.round(45 - dist * 28));
    }

    function isElementVisible(el) {
        if (!(el instanceof Element)) return false;

        const style = getComputedStyle(el);

        if (style.display === 'none') return false;
        if (style.visibility !== 'visible') return false;
        if (style.pointerEvents === 'none') return false;
        if (parseFloat(style.opacity || '1') < 0.05) return false;

        const rect = el.getBoundingClientRect();
        if (rect.width < CONFIG.minWidth || rect.height < CONFIG.minHeight) return false;

        return true;
    }

    function isDisabled(el) {
        if (!(el instanceof Element)) return true;
        if (el.hasAttribute('disabled')) return true;
        if (el.getAttribute('aria-disabled') === 'true') return true;
        return false;
    }

    function isActuallyClickable(el) {
        if (!(el instanceof Element)) return false;

        const tag = el.tagName.toLowerCase();

        if (tag === 'a' && el.hasAttribute('href')) return true;
        if (tag === 'button') return true;
        if (tag === 'summary') return true;
        if (tag === 'select') return true;
        if (tag === 'textarea') return true;
        if (tag === 'label') return true;
        if (tag === 'input') {
            const type = (el.getAttribute('type') || '').toLowerCase();
            return !['hidden'].includes(type);
        }

        if (el.hasAttribute('onclick')) return true;
        if (el.getAttribute('role') === 'button') return true;
        if (el.getAttribute('role') === 'link') return true;
        if (el.getAttribute('role') === 'menuitem') return true;
        if (el.getAttribute('role') === 'tab') return true;
        if (el.getAttribute('contenteditable') === 'true') return true;

        const tabindex = el.getAttribute('tabindex');
        if (tabindex !== null && tabindex !== '-1') return true;

        return false;
    }

    function getClickableAncestor(el) {
        let cur = el;
        let depth = 0;

        while (cur && depth < 6) {
            if (!(cur instanceof Element)) return null;
            if (isActuallyClickable(cur)) return cur;
            cur = cur.parentElement;
            depth++;
        }

        return null;
    }

    function elementFromCenterMatches(el) {
        const rect = el.getBoundingClientRect();

        const points = [
            [rect.left + rect.width / 2, rect.top + rect.height / 2],
            [rect.left + rect.width * 0.3, rect.top + rect.height * 0.3],
            [rect.left + rect.width * 0.7, rect.top + rect.height * 0.3],
            [rect.left + rect.width * 0.3, rect.top + rect.height * 0.7],
            [rect.left + rect.width * 0.7, rect.top + rect.height * 0.7]
        ];

        let hits = 0;

        for (const [x, y] of points) {
            if (x < 0 || y < 0 || x > window.innerWidth || y > window.innerHeight) continue;

            const topEl = document.elementFromPoint(x, y);
            if (!topEl) continue;

            if (topEl === el || el.contains(topEl) || topEl.contains(el)) {
                hits++;
            }
        }

        return hits >= 2;
    }

    function scoreElement(el) {
        let score = 0;
        const rect = el.getBoundingClientRect();
        const tag = el.tagName.toLowerCase();
        const text = normalizeText(el.innerText || el.textContent || '');

        if (tag === 'button') score += 40;
        if (tag === 'a') score += 35;
        if (tag === 'input') score += 30;
        if (tag === 'summary') score += 20;
        if (tag === 'select') score += 20;
        if (tag === 'textarea') score += 15;
        if (tag === 'label') score += 10;

        if (el.getAttribute('role') === 'button') score += 25;
        if (el.getAttribute('role') === 'link') score += 15;
        if (el.hasAttribute('onclick')) score += 20;
        if (el.hasAttribute('href')) score += 15;
        if (el.hasAttribute('tabindex')) score += 8;

        if (text.length > 0) score += 10;
        if (text.length >= 2 && text.length <= 20) score += 10;

        if (CONFIG.preferActionText) {
            if (/下载|立即|咨询|查看|进入|打开|继续|下一步|提交|详情|更多|购买|领取|开始|前往|跳转|申请|联系|客服|了解/.test(text)) {
                score += 30;
            }

            if (/关闭|取消|返回|跳过|收起|更多选项|菜单/.test(text)) {
                score -= 25;
            }
        }

        if (rect.width >= 24 && rect.height >= 24) score += 10;
        if (rect.width >= 60 && rect.height >= 20) score += 10;
        if (rect.width >= 90 && rect.height >= 28) score += 5;

        const areaBonus = calcAreaBonus(rect);
        score += areaBonus;

        return score;
    }

    function collectCandidates() {
        const selector = [
            'a',
            'button',
            'input',
            'select',
            'textarea',
            'summary',
            'label',
            '[role="button"]',
            '[role="link"]',
            '[role="menuitem"]',
            '[role="tab"]',
            '[onclick]',
            '[contenteditable="true"]',
            '[tabindex]',
            'div',
            'span'
        ].join(',');

        const all = [...document.querySelectorAll(selector)];
        const unique = new Map();

        for (const raw of all) {
            let el = getClickableAncestor(raw) || raw;

            if (!(el instanceof Element)) continue;
            if (unique.has(el)) continue;

            if (!isActuallyClickable(el)) continue;
            if (!isElementVisible(el)) continue;
            if (isDisabled(el)) continue;

            const rect = el.getBoundingClientRect();

            if (!isInViewport(rect)) continue;
            if (CONFIG.strictPreferredArea && !isInPreferredArea(rect)) continue;

            const topMost = elementFromCenterMatches(el);
            if (!topMost) continue;

            const { cx, cy } = getRectCenter(rect);
            const tag = el.tagName.toLowerCase();

            const info = {
                frameUrl: location.href,
                tagName: tag,
                text: normalizeText(el.innerText || el.textContent || ''),
                selector: buildCssSelector(el),
                xpath: buildXPath(el),
                left: rect.left,
                top: rect.top,
                width: rect.width,
                height: rect.height,
                centerX: cx,
                centerY: cy,
                score: scoreElement(el),
                isVisible: true,
                isTopMost: topMost,
                inViewport: true,
                inPreferredArea: isInPreferredArea(rect),
                enabled: true,
                editable: el.isContentEditable || tag === 'input' || tag === 'textarea'
            };

            unique.set(el, info);
        }

        return [...unique.values()]
            .sort((a, b) => {
                if (b.score !== a.score) return b.score - a.score;
                if (b.inPreferredArea !== a.inPreferredArea) return Number(b.inPreferredArea) - Number(a.inPreferredArea);
                return (b.width * b.height) - (a.width * a.height);
            })
            .slice(0, CONFIG.maxCount);
    }

    return JSON.stringify(collectCandidates());
}
""";
            }
        }
    


}
