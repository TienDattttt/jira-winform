using System.Runtime.CompilerServices;
using JiraClone.WinForms.Theme;

namespace JiraClone.WinForms.Helpers;

public static class LayoutHelper
{
    private static bool _isApplying;
    private static readonly ConditionalWeakTable<ComboBox, object?> ComboHooks = new();
    private static readonly ConditionalWeakTable<Button, object?> ButtonHooks = new();
    private static readonly ConditionalWeakTable<TextBox, object?> TextBoxHooks = new();
    private static readonly ConditionalWeakTable<Control, object?> ResponsiveRoots = new();
    private static readonly ConditionalWeakTable<Control, object?> ControlTreeHooks = new();

    public static void ConfigureForm(Form form)
    {
        form.AutoScaleMode = AutoScaleMode.Dpi;
        form.AutoScaleDimensions = new SizeF(96F, 96F);
    }

    public static int Scale(Control control, int originalValue) =>
        (int)Math.Round(originalValue * (control.DeviceDpi / 96.0));

    public static void EnableResponsiveLayout(Control root)
    {
        if (!ResponsiveRoots.TryGetValue(root, out _))
        {
            HookControlTree(root);
            ResponsiveRoots.Add(root, null);
        }

        ApplyControlTree(root);
    }

    public static void ConfigureButton(Button button, int horizontalPadding = 20, int verticalPadding = 12)
    {
        if (!ButtonHooks.TryGetValue(button, out _))
        {
            button.TextChanged += OnButtonMetricsChanged;
            button.FontChanged += OnButtonMetricsChanged;
            button.HandleCreated += OnButtonMetricsChanged;
            ButtonHooks.Add(button, null);
        }

        button.Padding = new Padding(
            Math.Max(button.Padding.Left, horizontalPadding / 2),
            Math.Max(button.Padding.Top, verticalPadding / 2),
            Math.Max(button.Padding.Right, horizontalPadding / 2),
            Math.Max(button.Padding.Bottom, verticalPadding / 2));
        EnsureButtonFits(button, horizontalPadding, verticalPadding);
    }

    public static void ConfigureTextBox(TextBox textBox)
    {
        if (!TextBoxHooks.TryGetValue(textBox, out _))
        {
            textBox.FontChanged += OnTextBoxMetricsChanged;
            textBox.HandleCreated += OnTextBoxMetricsChanged;
            TextBoxHooks.Add(textBox, null);
        }

        EnsureTextBoxFits(textBox);
    }

    public static void ConfigureComboBox(ComboBox comboBox)
    {
        comboBox.IntegralHeight = false;
        if (!ComboHooks.TryGetValue(comboBox, out _))
        {
            comboBox.DropDown += OnComboBoxNeedsLayout;
            comboBox.FontChanged += OnComboBoxNeedsLayout;
            comboBox.HandleCreated += OnComboBoxNeedsLayout;
            comboBox.DataSourceChanged += OnComboBoxNeedsLayout;
            comboBox.TextChanged += OnComboBoxNeedsLayout;
            ComboHooks.Add(comboBox, null);
        }

        EnsureComboBoxFits(comboBox);
    }

    public static void UpdateDropDownWidth(ComboBox comboBox)
    {
        if (comboBox.Items.Count == 0)
        {
            comboBox.DropDownWidth = Math.Max(comboBox.DropDownWidth, comboBox.Width);
            return;
        }

        var maxWidth = comboBox.Items.Cast<object>()
            .Select(item => TextRenderer.MeasureText(item?.ToString() ?? string.Empty, comboBox.Font).Width)
            .DefaultIfEmpty(comboBox.Width)
            .Max();
        comboBox.DropDownWidth = Math.Max(maxWidth + Scale(comboBox, 28), comboBox.Width);
    }

    private static void HookControlTree(Control control)
    {
        if (ControlTreeHooks.TryGetValue(control, out _))
        {
            return;
        }

        control.ControlAdded += OnParentControlAdded;
        control.FontChanged += OnGenericLayoutInvalidated;
        control.TextChanged += OnGenericLayoutInvalidated;
        ControlTreeHooks.Add(control, null);

        foreach (Control child in control.Controls)
        {
            HookControlTree(child);
        }
    }

    private static void OnParentControlAdded(object? sender, ControlEventArgs e)
    {
        if (e.Control is null)
        {
            return;
        }

        HookControlTree(e.Control);
        ApplyControlTree(e.Control);
        if (sender is Control parent)
        {
            EnsureContainerFitsChildren(parent);
        }
    }

    private static void OnGenericLayoutInvalidated(object? sender, EventArgs e)
    {
        if (_isApplying)
        {
            return;
        }

        if (sender is Control control)
        {
            ApplyControl(control);
        }
    }

    private static void ApplyControlTree(Control control)
    {
        if (_isApplying)
        {
            return;
        }

        _isApplying = true;
        try
        {
            ApplyControlTreeCore(control);
        }
        finally
        {
            _isApplying = false;
        }
    }

    private static void ApplyControl(Control control)
    {
        if (_isApplying)
        {
            return;
        }

        _isApplying = true;
        try
        {
            ApplyControlCore(control);
        }
        finally
        {
            _isApplying = false;
        }
    }

    private static void ApplyControlTreeCore(Control control)
    {
        ApplyControlCore(control);
        foreach (Control child in control.Controls)
        {
            ApplyControlTreeCore(child);
        }

        EnsureContainerFitsChildrenCore(control);
    }

    private static void ApplyControlCore(Control control)
    {
        if (control is Form form)
        {
            ConfigureForm(form);
        }

        switch (control)
        {
            case Label label:
                EnsureLabelFits(label);
                break;
            case Button button:
                ConfigureButton(button);
                break;
            case TextBox textBox:
                ConfigureTextBox(textBox);
                break;
            case ComboBox comboBox:
                ConfigureComboBox(comboBox);
                UpdateDropDownWidth(comboBox);
                break;
        }
    }

    private static void EnsureLabelFits(Label label)
    {
        if (label.Dock == DockStyle.Fill)
        {
            label.AutoEllipsis = true;
            return;
        }

        if (label.Dock == DockStyle.None || label.MaximumSize.Width > 0)
        {
            label.AutoSize = true;
        }
        else if (!label.AutoSize)
        {
            label.AutoEllipsis = true;
        }
    }

    private static void EnsureContainerFitsChildren(Control? container)
    {
        if (_isApplying || container is null || container.IsDisposed || container.Controls.Count == 0)
        {
            return;
        }

        _isApplying = true;
        try
        {
            EnsureContainerFitsChildrenCore(container);
        }
        finally
        {
            _isApplying = false;
        }
    }

    private static void EnsureContainerFitsChildrenCore(Control container)
    {
        var contentHeight = container.Padding.Top + container.Padding.Bottom;
        var contentWidth = container.Padding.Left + container.Padding.Right;
        foreach (Control child in container.Controls)
        {
            if (!child.Visible)
            {
                continue;
            }

            contentHeight = Math.Max(contentHeight, child.Bottom + child.Margin.Bottom + container.Padding.Bottom);
            contentWidth = Math.Max(contentWidth, child.Right + child.Margin.Right + container.Padding.Right);
        }

        if (container.Dock is DockStyle.Top or DockStyle.Bottom)
        {
            container.Height = Math.Max(container.Height, contentHeight);
        }

        // Do NOT grow width for docked-Left/Right panels — it pushes them off-screen.
        // Only grow panels that are not docked or explicitly AutoSize.
        if (container is FlowLayoutPanel flow && !flow.WrapContents)
        {
            if (flow.Dock is DockStyle.Top or DockStyle.Bottom)
            {
                flow.Height = Math.Max(flow.Height, contentHeight);
            }
            else if (flow.Dock is DockStyle.None && flow.AutoSize)
            {
                var totalWidth = flow.Padding.Left + flow.Padding.Right;
                foreach (Control child in flow.Controls)
                {
                    if (!child.Visible)
                    {
                        continue;
                    }

                    totalWidth += child.Width + child.Margin.Horizontal;
                }
                flow.Width = Math.Max(flow.Width, totalWidth);
            }
        }
    }

    private static void OnButtonMetricsChanged(object? sender, EventArgs e)
    {
        if (_isApplying)
        {
            return;
        }

        if (sender is Button button)
        {
            _isApplying = true;
            try
            {
            EnsureButtonFits(button, 20, 12);
            }
            finally
            {
                _isApplying = false;
            }
        }
    }

    private static void OnTextBoxMetricsChanged(object? sender, EventArgs e)
    {
        if (_isApplying)
        {
            return;
        }

        if (sender is TextBox textBox)
        {
            _isApplying = true;
            try
            {
            EnsureTextBoxFits(textBox);
            }
            finally
            {
                _isApplying = false;
            }
        }
    }

    private static void OnComboBoxNeedsLayout(object? sender, EventArgs e)
    {
        if (_isApplying)
        {
            return;
        }

        if (sender is ComboBox comboBox)
        {
            _isApplying = true;
            try
            {
            EnsureComboBoxFits(comboBox);
            UpdateDropDownWidth(comboBox);
            }
            finally
            {
                _isApplying = false;
            }
        }
    }

    private static void EnsureButtonFits(Button button, int horizontalPadding, int verticalPadding)
    {
        var textSize = TextRenderer.MeasureText(button.Text ?? string.Empty, button.Font);
        var minWidth = textSize.Width + horizontalPadding + button.Padding.Horizontal;
        var minHeight = textSize.Height + verticalPadding + button.Padding.Vertical;
        var minimumSize = new Size(minWidth, minHeight);
        button.MinimumSize = new Size(
            Math.Max(button.MinimumSize.Width, minimumSize.Width),
            Math.Max(button.MinimumSize.Height, minimumSize.Height));

        if (!button.AutoSize)
        {
            button.Size = new Size(
                Math.Max(button.Width, button.MinimumSize.Width),
                Math.Max(button.Height, button.MinimumSize.Height));
        }
    }

    private static void EnsureTextBoxFits(TextBox textBox)
    {
        if (textBox.Multiline)
        {
            return;
        }

        var textSize = TextRenderer.MeasureText("Mg", textBox.Font);
        var minHeight = textSize.Height + Scale(textBox, 14);
        textBox.MinimumSize = new Size(Math.Max(textBox.MinimumSize.Width, Scale(textBox, 120)), Math.Max(textBox.MinimumSize.Height, minHeight));
        textBox.Height = Math.Max(textBox.Height, textBox.MinimumSize.Height);
    }

    private static void EnsureComboBoxFits(ComboBox comboBox)
    {
        var textSize = TextRenderer.MeasureText("Mg", comboBox.Font);
        var minHeight = textSize.Height + Scale(comboBox, 16);
        comboBox.MinimumSize = new Size(Math.Max(comboBox.MinimumSize.Width, comboBox.Width), Math.Max(comboBox.MinimumSize.Height, minHeight));
        comboBox.Height = Math.Max(comboBox.Height, comboBox.MinimumSize.Height);
    }
}


