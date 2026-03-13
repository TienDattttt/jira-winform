namespace JiraClone.WinForms.Services;

public static class ErrorDialogService
{
    public static void Show(Exception exception) =>
        MessageBox.Show(exception.Message, "Unexpected Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

    public static void Show(string message) =>
        MessageBox.Show(message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
}
