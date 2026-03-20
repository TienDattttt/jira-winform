using JiraClone.WinForms.Helpers;

namespace JiraClone.WinForms.Services;

public static class ErrorDialogService
{
    public static void Show(Exception exception) =>
        MessageBox.Show(
            VietnameseUi.Translate(exception.Message),
            VietnameseUi.Translate("Unexpected Error"),
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);

    public static void Show(string message) =>
        MessageBox.Show(
            VietnameseUi.Translate(message),
            VietnameseUi.Translate("Error"),
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
}
