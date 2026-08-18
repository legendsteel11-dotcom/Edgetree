using System.Windows;

namespace SidebarExplorer.App.Services;

// 메뉴 줄이 체크 칸을 비워 둘지. 메뉴가 열릴 때 코드가 정한다.
//
// 왜 코드인가: 이 판단은 줄 하나만 봐서는 못 한다. "이 메뉴에 체크 가능한 줄이
// 하나라도 있는가"를 묻는 것이라 형제들을 함께 봐야 하고, XAML 트리거는 자기
// 자신밖에 못 본다.
//
// 왜 Tag 가 아닌가: 템플릿에 Tag="reserve-check-column" 트리거가 있었고 그것이
// 원래 이 자리였는데, 이 앱은 Tag 를 메뉴 항목 식별자로 이미 59곳에서 쓴다
// (Tag="sort", Tag="asc" ...). 손으로 붙이던 동안에는 겹치지 않는 줄만 골라
// 쓰면 됐지만, 전부에 자동으로 붙이는 순간 그 식별자를 덮어쓴다.
public static class MenuVisual
{
    public static readonly DependencyProperty ReserveCheckProperty =
        DependencyProperty.RegisterAttached(
            "ReserveCheck",
            typeof(bool),
            typeof(MenuVisual),
            new FrameworkPropertyMetadata(false));

    public static void SetReserveCheck(DependencyObject element, bool value)
        => element.SetValue(ReserveCheckProperty, value);

    public static bool GetReserveCheck(DependencyObject element)
        => (bool)element.GetValue(ReserveCheckProperty);
}
