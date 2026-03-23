using System.Windows.Forms;

namespace JiraClone.WinForms.Helpers;

public static class SplitContainerHelper
{
    public static void ConfigureSafeLayout(SplitContainer splitContainer, int preferredDistance, int minPanel1 = 0, int minPanel2 = 0)
    {
        if (splitContainer.IsDisposed)
        {
            return;
        }

        var panelSpan = splitContainer.Orientation == Orientation.Vertical
            ? splitContainer.ClientSize.Width
            : splitContainer.ClientSize.Height;
        var usableSpan = panelSpan - splitContainer.SplitterWidth;
        if (usableSpan <= 0)
        {
            return;
        }

        minPanel1 = Math.Max(0, minPanel1);
        minPanel2 = Math.Max(0, minPanel2);

        if (minPanel1 + minPanel2 > usableSpan)
        {
            var overflow = minPanel1 + minPanel2 - usableSpan;
            if (minPanel2 >= overflow)
            {
                minPanel2 -= overflow;
            }
            else
            {
                overflow -= minPanel2;
                minPanel2 = 0;
                minPanel1 = Math.Max(0, minPanel1 - overflow);
            }
        }

        try
        {
            if (splitContainer.Panel1MinSize != 0)
            {
                splitContainer.Panel1MinSize = 0;
            }

            if (splitContainer.Panel2MinSize != 0)
            {
                splitContainer.Panel2MinSize = 0;
            }
        }
        catch (ArgumentOutOfRangeException)
        {
            return;
        }
        catch (InvalidOperationException)
        {
            return;
        }

        var maxPanel1 = usableSpan - minPanel2;
        if (maxPanel1 < minPanel1)
        {
            return;
        }

        var safeDistance = Math.Max(minPanel1, Math.Min(preferredDistance, maxPanel1));
        if (safeDistance <= 0)
        {
            return;
        }

        try
        {
            if (splitContainer.SplitterDistance != safeDistance)
            {
                splitContainer.SplitterDistance = safeDistance;
            }

            if (splitContainer.Panel1MinSize != minPanel1)
            {
                splitContainer.Panel1MinSize = minPanel1;
            }

            if (splitContainer.Panel2MinSize != minPanel2)
            {
                splitContainer.Panel2MinSize = minPanel2;
            }

            if (splitContainer.SplitterDistance != safeDistance)
            {
                splitContainer.SplitterDistance = safeDistance;
            }
        }
        catch (ArgumentOutOfRangeException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    public static void ApplySafeSplitterDistance(SplitContainer splitContainer, int preferredDistance)
    {
        ConfigureSafeLayout(splitContainer, preferredDistance, splitContainer.Panel1MinSize, splitContainer.Panel2MinSize);
    }
}
