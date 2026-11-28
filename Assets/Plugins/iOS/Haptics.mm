// Вибрация на iOS через Taptic Engine. Вызывается из Assets/Scripts/Game/Haptics.cs.
#import <UIKit/UIKit.h>

extern "C" {

// style: 0 — лёгкий, 1 — средний, 2 — сильный.
void AshHapticImpact(int style)
{
    UIImpactFeedbackStyle s = style <= 0 ? UIImpactFeedbackStyleLight
                            : style == 1 ? UIImpactFeedbackStyleMedium
                                         : UIImpactFeedbackStyleHeavy;
    UIImpactFeedbackGenerator *generator = [[UIImpactFeedbackGenerator alloc] initWithStyle:s];
    [generator prepare];
    [generator impactOccurred];
}

// type: 0 — успех, 1 — предупреждение, 2 — ошибка.
void AshHapticNotify(int type)
{
    UINotificationFeedbackType t = type <= 0 ? UINotificationFeedbackTypeSuccess
                                 : type == 1 ? UINotificationFeedbackTypeWarning
                                             : UINotificationFeedbackTypeError;
    UINotificationFeedbackGenerator *generator = [[UINotificationFeedbackGenerator alloc] init];
    [generator prepare];
    [generator notificationOccurred:t];
}

}
