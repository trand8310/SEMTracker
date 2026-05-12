using Microsoft.Playwright;
using System.Diagnostics;


namespace BDInsight
{
    public class BDInsightHelper
    {
        public static async Task<bool> IsElementInViewportAsync(ILocator locator)
        {
            if (!await locator.IsVisibleAsync())
            {
                return false;
            }
            return await locator.EvaluateAsync<bool>(@"(element) => {
            const rect = element.getBoundingClientRect();
            return (
              rect.top >= 0 &&
              rect.left >= 0 &&
              rect.bottom <= (window.innerHeight || document.documentElement.clientHeight) &&
              rect.right <= (window.innerWidth || document.documentElement.clientWidth));
             }");
        }

        public static async Task<List<ILocator>> GetVisibleElementsAsync(ILocator locator)
        {
            var result = new List<ILocator>();

            int count = await locator.CountAsync();
            if (count == 0)
                return result;
            for (int i = 0; i < count; i++)
            {
                var el = locator.Nth(i);
                if (await IsElementInViewportAsync(el))
                {
                    result.Add(el);
                }
            }
            return result;
        }



        public static async Task<ILocator?> WaitVisibleLocatorAsync(
        IEnumerable<ILocator> locators,
        CancellationToken token,
        int timeoutMs = 10000,
        int intervalMs = 250)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                token.ThrowIfCancellationRequested();
                foreach (var locator in locators)
                {
                    try
                    {
                        var first = locator.First;
                        if (await first.CountAsync() > 0 && await first.IsVisibleAsync())
                        {
                            return first;
                        }
                    }
                    catch
                    {

                    }
                }
                await Task.Delay(intervalMs, token);
            }
            return null;
        }


        public sealed class PageScrollState
        {
            public double ScrollY { get; set; }
            public double ClientHeight { get; set; }
            public double ScrollHeight { get; set; }
            public bool CanScrollDown { get; set; }
        }

        /// <summary>
        /// 获取页面滑动后的状态
        /// </summary>
        /// <param name="page"></param>
        /// <returns></returns>
        public static async Task<PageScrollState> GetPageScrollStateAsync(IPage page)
        {
            try
            {
                return await page.EvaluateAsync<PageScrollState>(@"() => {
                    const doc = document.documentElement;
                    const body = document.body;

                    const scrollY = window.scrollY || window.pageYOffset || doc.scrollTop || body?.scrollTop || 0;
                    const clientHeight = window.innerHeight || doc.clientHeight || body?.clientHeight || 0;
                    const scrollHeight = Math.max(
                        doc.scrollHeight || 0,
                        body?.scrollHeight || 0,
                        doc.offsetHeight || 0,
                        body?.offsetHeight || 0,
                        doc.clientHeight || 0
                    );

                    // 留一点容差，避免小数误差导致明明到底了还继续滑
                    const canScrollDown = (scrollY + clientHeight) < (scrollHeight - 2);

                    return {
                        scrollY,
                        clientHeight,
                        scrollHeight,
                        canScrollDown
                    };
                }");
            }
            catch
            {
                return new PageScrollState
                {
                    ScrollY = 0,
                    ClientHeight = 0,
                    ScrollHeight = 0,
                    CanScrollDown = false
                };
            }
        }



        public static async Task<List<IElementHandle>> GetCurrentViewportClickableElementsAsync(IPage page, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            var clickableHandles = await page.EvaluateHandleAsync(@"() => {
                const all = Array.from(document.querySelectorAll('*'));
                const visible = all.filter(el => {
                    const style = window.getComputedStyle(el);
                    const rect = el.getBoundingClientRect();
                    return style.visibility !== 'hidden' &&
                           style.display !== 'none' &&
                           rect.width > 0 &&
                           rect.height > 0 &&
                           rect.top >= 0 &&
                           rect.left >= 0 &&
                           rect.bottom <= window.innerHeight &&
                           rect.right <= window.innerWidth;
                });

                return visible.filter(el => {
                    const rect = el.getBoundingClientRect();
                    const x = rect.left + rect.width / 2;
                    const y = rect.top + rect.height / 2;
                    const topEl = document.elementFromPoint(x, y);
                    const hasClick = el.onclick || el.tagName === 'A' || el.tagName === 'BUTTON' || el.getAttribute('role') === 'button';
                    const notCovered = topEl && (el === topEl || el.contains(topEl));
                    return hasClick && notCovered;
                });
            }");

            var props = await clickableHandles.GetPropertiesAsync();
            var elements = new List<IElementHandle>();

            foreach (var p in props.Values)
            {
                token.ThrowIfCancellationRequested();

                var el = p.AsElement();
                if (el != null)
                    elements.Add(el);
            }

            return elements;
        }




    }
}
