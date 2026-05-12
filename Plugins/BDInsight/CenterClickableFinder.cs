namespace BDInsight
{
    using Microsoft.Playwright;
    using System.Text;

    public sealed class CenterClickCandidate
    {
        public string TagName { get; set; } = "";
        public string SelectorHint { get; set; } = "";
        public double CenterX { get; set; }
        public double CenterY { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Score { get; set; }
    }

    public static class CenterClickableFinder
    {
        private const string MarkerAttr = "data-oai-click-candidate";

        public static async Task<List<CenterClickCandidate>> GetCandidatesAsync(
            IPage page,
            double xMinRatio = 0.30,
            double xMaxRatio = 0.70,
            double yMinRatio = 0.30,
            double yMaxRatio = 0.70,
            int xSteps = 5,
            int ySteps = 5)
        {
            if (page == null || page.IsClosed || page.ViewportSize == null)
                return new List<CenterClickCandidate>();

            NormalizeArgs(
                page,
                ref xMinRatio,
                ref xMaxRatio,
                ref yMinRatio,
                ref yMaxRatio,
                ref xSteps,
                ref ySteps,
                out int vw,
                out int vh);

            var script = BuildFinderScript(returnMarkedCount: false);

            var result = await page.EvaluateAsync<List<CenterClickCandidate>>(
                script,
                new object[] { vw, vh, xMinRatio, xMaxRatio, yMinRatio, yMaxRatio, xSteps, ySteps, MarkerAttr });

            return result ?? new List<CenterClickCandidate>();
        }

        public static async Task<int> MarkCandidatesAsync(
            IPage page,
            double xMinRatio = 0.30,
            double xMaxRatio = 0.70,
            double yMinRatio = 0.30,
            double yMaxRatio = 0.70,
            int xSteps = 5,
            int ySteps = 5)
        {
            if (page == null || page.IsClosed || page.ViewportSize == null)
                return 0;

            NormalizeArgs(
                page,
                ref xMinRatio,
                ref xMaxRatio,
                ref yMinRatio,
                ref yMaxRatio,
                ref xSteps,
                ref ySteps,
                out int vw,
                out int vh);

            var script = BuildFinderScript(returnMarkedCount: true);

            return await page.EvaluateAsync<int>(
                script,
                new object[] { vw, vh, xMinRatio, xMaxRatio, yMinRatio, yMaxRatio, xSteps, ySteps, MarkerAttr });
        }

        public static async Task<CenterClickCandidate?> GetBestCandidateAsync(
            IPage page,
            double xMinRatio = 0.30,
            double xMaxRatio = 0.70,
            double yMinRatio = 0.30,
            double yMaxRatio = 0.70,
            int xSteps = 5,
            int ySteps = 5)
        {
            var list = await GetCandidatesAsync(page, xMinRatio, xMaxRatio, yMinRatio, yMaxRatio, xSteps, ySteps);
            return list.Count > 0 ? list[0] : null;
        }

        public static async Task<bool> ClickBestByMouseAsync(
            IPage page,
            CancellationToken cancellationToken = default)
        {
            if (page == null || page.IsClosed)
                return false;

            var best = await GetBestCandidateAsync(page);
            if (best == null)
                return false;

            try
            {
                var x = (float)GetSafeInnerPoint(best.Width, best.CenterX);
                var y = (float)GetSafeInnerPoint(best.Height, best.CenterY);

                await page.Mouse.MoveAsync(x, y);
                await Task.Delay(Random.Shared.Next(35, 90), cancellationToken);
                await page.Mouse.ClickAsync(x, y);

                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<bool> ClickBestByTouchAsync(
            IPage page,
            ICDPSession client,
            CancellationToken cancellationToken = default)
        {
            if (page == null || page.IsClosed || client == null)
                return false;

            var best = await GetBestCandidateAsync(page);
            if (best == null)
                return false;

            try
            {
                var viewport = page.ViewportSize;
                if (viewport == null)
                    return false;

                float x = (float)Math.Clamp(
                    GetSafeInnerPoint(best.Width, best.CenterX),
                    1,
                    viewport.Width - 1);

                float y = (float)Math.Clamp(
                    GetSafeInnerPoint(best.Height, best.CenterY),
                    1,
                    viewport.Height - 1);

                await client.SendAsync("Input.dispatchTouchEvent", new Dictionary<string, object>
                {
                    ["type"] = "touchStart",
                    ["touchPoints"] = new object[]
                    {
                        new { x, y }
                    },
                    ["modifiers"] = 0
                });

                await Task.Delay(Random.Shared.Next(35, 70), cancellationToken);

                await client.SendAsync("Input.dispatchTouchEvent", new Dictionary<string, object>
                {
                    ["type"] = "touchEnd",
                    ["touchPoints"] = Array.Empty<object>()
                });

                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch
            {
                return false;
            }
        }

        public static ILocator GetMarkedLocator(IPage page)
        {
            return page.Locator($"[{MarkerAttr}='1']");
        }

        private static void NormalizeArgs(
            IPage page,
            ref double xMinRatio,
            ref double xMaxRatio,
            ref double yMinRatio,
            ref double yMaxRatio,
            ref int xSteps,
            ref int ySteps,
            out int vw,
            out int vh)
        {
            vw = page.ViewportSize!.Width;
            vh = page.ViewportSize!.Height;

            xMinRatio = ClampRatio(xMinRatio);
            xMaxRatio = ClampRatio(xMaxRatio);
            yMinRatio = ClampRatio(yMinRatio);
            yMaxRatio = ClampRatio(yMaxRatio);

            if (xMinRatio > xMaxRatio)
                (xMinRatio, xMaxRatio) = (xMaxRatio, xMinRatio);

            if (yMinRatio > yMaxRatio)
                (yMinRatio, yMaxRatio) = (yMaxRatio, yMinRatio);

            xSteps = Math.Clamp(xSteps, 2, 9);
            ySteps = Math.Clamp(ySteps, 2, 9);
        }

        private static double ClampRatio(double value)
        {
            if (value < 0) return 0;
            if (value > 1) return 1;
            return value;
        }

        /// <summary>
        /// 在候选框内部选一个更像真人的安全点击点。
        /// 这里传入的是中心点坐标，所以需要把随机偏移加回去。
        /// </summary>
        private static double GetSafeInnerPoint(double size, double center)
        {
            if (size <= 2)
                return center;

            // 取中间 20% 范围内的微随机，不点边缘
            double half = size / 2.0;
            double offset = Random.Shared.NextDouble() * (half * 0.20 * 2) - (half * 0.20);
            return center + offset;
        }

        private static string BuildFinderScript(bool returnMarkedCount)
        {
            var sb = new StringBuilder();

            sb.Append(
@"([vw, vh, xMinRatio, xMaxRatio, yMinRatio, yMaxRatio, xSteps, ySteps, markerAttr]) => {
    document.querySelectorAll('[' + markerAttr + '=""1""]').forEach(el => el.removeAttribute(markerAttr));

    function buildPercents(min, max, steps) {
        if (steps <= 1) return [(min + max) / 2];
        const arr = [];
        const span = max - min;
        for (let i = 0; i < steps; i++) {
            arr.push(min + span * (i / (steps - 1)));
        }
        return arr;
    }

    const xPercents = buildPercents(xMinRatio, xMaxRatio, xSteps);
    const yPercents = buildPercents(yMinRatio, yMaxRatio, ySteps);

    const results = [];
    const seen = new Set();
    let markedCount = 0;

    function tryParseUrl(raw) {
        if (!raw || typeof raw !== 'string') return null;
        const value = raw.trim();
        if (!value) return null;

        try {
            return new URL(value, location.href);
        } catch {
            return null;
        }
    }

    function isHttpProtocol(urlObj) {
        if (!urlObj || !urlObj.protocol) return false;
        const p = String(urlObj.protocol).toLowerCase();
        return p === 'http:' || p === 'https:';
    }

    function containsAny(text, keywords) {
        if (!text) return false;
        const s = String(text).toLowerCase();
        for (const k of keywords) {
            if (s.includes(k)) return true;
        }
        return false;
    }

    function isBlockedHost(hostname) {
        if (!hostname) return false;
        const h = String(hostname).toLowerCase();

        const exactBlockedHosts = [
            'work.weixin.qq.com',
            'weixin.qq.com',
            'open.weixin.qq.com',
            'qm.qq.com',
            'w.url.cn',
            'c.pc.qq.com',
            'connect.qq.com',
            'graph.qq.com'
        ];

        if (exactBlockedHosts.includes(h))
            return true;

        const suffixBlocked = [
            '.work.weixin.qq.com',
            '.weixin.qq.com',
            '.open.weixin.qq.com',
            '.qm.qq.com',
            '.url.cn'
        ];

        for (const s of suffixBlocked) {
            if (h.endsWith(s)) return true;
        }

        return false;
    }

    function isBlockedHttpAppLink(urlObj) {
        if (!urlObj) return false;
        if (!isHttpProtocol(urlObj)) return true;
        if (isBlockedHost(urlObj.hostname)) return true;

        const path = (urlObj.pathname || '').toLowerCase();
        const search = (urlObj.search || '').toLowerCase();
        const hash = (urlObj.hash || '').toLowerCase();
        const full = path + search + hash;

        const blockedPathParts = [
            '/ca/',
            '/cgi-bin/',
            '/deeplink',
            '/deep-link',
            '/deep_link',
            '/openapp',
            '/launch',
            '/jump',
            '/invoke',
            '/universal-link'
        ];

        for (const p of blockedPathParts) {
            if (path.includes(p)) return true;
        }

        const blockedKeywords = [
            'weixin',
            'wechat',
            'wxwork',
            'mqq',
            'qqlaunch',
            'customer_channel=',
            'scheme=',
            'scheme%3a',
            'deeplink',
            'deep_link',
            'openapp',
            'launchapp',
            'launch=',
            'intent:',
            'weixin:',
            'wxwork:',
            'mqq:',
            'qq:'
        ];

        if (containsAny(full, blockedKeywords))
            return true;

        return false;
    }

    function isSafeWebUrl(raw) {
        if (!raw || typeof raw !== 'string')
            return true;

        const value = raw.trim();
        if (!value)
            return true;

        const urlObj = tryParseUrl(value);
        if (!urlObj)
            return true;

        return !isBlockedHttpAppLink(urlObj);
    }

    function hasBlockedUrlAttribute(el) {
        if (!el || !el.getAttribute) return false;

        const attrs = [
            'href',
            'data-href',
            'data-url',
            'data-link',
            'data-jump',
            'data-redirect',
            'data-target-url',
            'formaction',
            'action',
            'xlink:href'
        ];

        for (const name of attrs) {
            const val = el.getAttribute(name);
            if (!val) continue;

            if (!isSafeWebUrl(val))
                return true;
        }

        return false;
    }

    function hasBlockedOnclick(el) {
        if (!el || !el.getAttribute) return false;

        const onclickText = el.getAttribute('onclick') || '';
        if (!onclickText) return false;

        const s = onclickText.toLowerCase();

        const blockedSnippets = [
            'javascript:',
            'mailto:',
            'tel:',
            'sms:',
            'intent:',
            'weixin:',
            'wxwork:',
            'wechat:',
            'mqq:',
            'qq:',
            'alipays:',
            'openapp',
            'launchapp',
            'deeplink',
            'deep_link'
        ];

        if (containsAny(s, blockedSnippets))
            return true;

        if ((s.includes('location.href') || s.includes('window.open') || s.includes('open(')) &&
            containsAny(s, ['weixin', 'wechat', 'wxwork', 'qq', 'mqq']))
            return true;

        return false;
    }

    function isAllowedTarget(el) {
        if (!el) return false;

        const linkLike = el.closest('a, area, form');
        if (linkLike && hasBlockedUrlAttribute(linkLike))
            return false;

        if (hasBlockedUrlAttribute(el))
            return false;

        if (hasBlockedOnclick(el))
            return false;

        return true;
    }

    function isVisible(el) {
        if (!el) return false;

        const style = getComputedStyle(el);
        if (style.display === 'none') return false;
        if (style.visibility === 'hidden') return false;
        if (style.pointerEvents === 'none') return false;
        if (parseFloat(style.opacity || '1') <= 0.05) return false;

        const rect = el.getBoundingClientRect();
        if (!rect) return false;
        if (rect.width < 2 || rect.height < 2) return false;
        if (rect.bottom <= 0 || rect.right <= 0 || rect.left >= vw || rect.top >= vh) return false;

        return true;
    }

    function isReasonableArea(el) {
        const rect = el.getBoundingClientRect();
        if (!rect) return false;

        const area = rect.width * rect.height;
        if (area < 36) return false;

        // 太大的容器通常不是理想点击目标
        if (rect.width > vw * 0.95 && rect.height > vh * 0.45) return false;

        return true;
    }

    function isClickable(el) {
        if (!el) return false;
        if (el.disabled) return false;
        if (!isAllowedTarget(el)) return false;

        const tag = (el.tagName || '').toLowerCase();
        if (tag === 'html' || tag === 'body') return false;

        if (tag === 'a' || tag === 'area') {
            const href = el.getAttribute('href') || '';
            if (!href) return false;
            return isSafeWebUrl(href);
        }

        if (tag === 'button' || tag === 'summary' || tag === 'label') return true;
        if (tag === 'input' && el.type !== 'hidden') return true;

        const role = (el.getAttribute('role') || '').toLowerCase();
        if (role === 'button' || role === 'link' || role === 'menuitem' || role === 'tab')
            return true;

        if (el.hasAttribute('onclick')) return true;
        if (el.hasAttribute('tabindex') && el.tabIndex >= 0) return true;
        if (el.hasAttribute('aria-expanded')) return true;
        if (el.hasAttribute('aria-pressed')) return true;

        const style = getComputedStyle(el);
        if (style.cursor === 'pointer') return true;

        return false;
    }

    function getClickableAncestor(el) {
        let cur = el;
        while (cur && cur !== document.documentElement) {
            if (isVisible(cur) && isClickable(cur) && isReasonableArea(cur)) {
                return cur;
            }
            cur = cur.parentElement;
        }
        return null;
    }

    function getSelectorHint(el) {
        if (!el) return '';

        const tag = (el.tagName || '').toLowerCase();
        const id = el.id ? ('#' + el.id) : '';
        const cls = el.classList && el.classList.length > 0
            ? '.' + Array.from(el.classList).slice(0, 2).join('.')
            : '';

        const dataType = el.getAttribute('data-type');
        const dt = dataType ? ('[data-type=""' + dataType + '""]') : '';

        return '' + tag + id + cls + dt;
    }

    function addResult(hit) {
        const rect = hit.getBoundingClientRect();

        const key =
            (hit.tagName || '') + '|' +
            (hit.id || '') + '|' +
            (hit.className || '') + '|' +
            Math.round(rect.left) + '|' +
            Math.round(rect.top) + '|' +
            Math.round(rect.width) + '|' +
            Math.round(rect.height);

        if (seen.has(key))
            return false;

        seen.add(key);

        const cx = rect.left + rect.width / 2;
        const cy = rect.top + rect.height / 2;

        const dx = Math.abs(cx - vw / 2);
        const dy = Math.abs(cy - vh / 2);

        let score = 1000 - dx * 2 - dy * 2;

        const area = rect.width * rect.height;
        if (area < 100) score -= 120;
        else if (area < 180) score -= 50;
        else if (area > vw * vh * 0.20) score -= 120;

        const tag = (hit.tagName || '').toLowerCase();
        if (tag === 'button' || tag === 'a') score += 40;
        if (tag === 'input') score += 20;

        results.push({
            tagName: tag,
            selectorHint: getSelectorHint(hit),
            centerX: cx,
            centerY: cy,
            width: rect.width,
            height: rect.height,
            score: score
        });

        return true;
    }

    for (const xp of xPercents) {
        for (const yp of yPercents) {
            const x = Math.round(vw * xp);
            const y = Math.round(vh * yp);

            const stack = document.elementsFromPoint(x, y);
            if (!stack || stack.length === 0) continue;

            for (const raw of stack) {
                const hit = getClickableAncestor(raw);
                if (!hit) continue;

                if (addResult(hit)) {
                    hit.setAttribute(markerAttr, '1');
                    markedCount++;
                }

                break;
            }
        }
    }

    results.sort((a, b) => b.score - a.score);
");

            if (returnMarkedCount)
            {
                sb.Append("return markedCount;");
            }
            else
            {
                sb.Append("return results;");
            }

            sb.Append("}");

            return sb.ToString();
        }
    }
}