import 'package:flutter/foundation.dart';

/// Whether the app runs on a phone or tablet OS, native or in its browser.
/// Input there is assumed to be touch, where a press-and-move is usually a
/// scroll rather than a drag.
bool get isTouchPlatform => switch (defaultTargetPlatform) {
  TargetPlatform.android || TargetPlatform.iOS => true,
  _ => false,
};
