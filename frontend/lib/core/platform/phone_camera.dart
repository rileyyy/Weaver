import 'package:flutter/foundation.dart';

/// Whether the app runs as a native phone app, which can open the system
/// camera. A phone's browser can't take a photo through image_picker, and
/// desktops have no system camera UI, so both are excluded.
bool hasPhoneCamera({required bool isWeb, required TargetPlatform platform}) =>
    !isWeb &&
    switch (platform) {
      TargetPlatform.android || TargetPlatform.iOS => true,
      _ => false,
    };
