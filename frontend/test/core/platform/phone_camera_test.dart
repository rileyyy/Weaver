import 'package:flutter/foundation.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/core/platform/phone_camera.dart';

void main() {
  test('native Android and iOS apps have a camera to open', () {
    for (final platform in [TargetPlatform.android, TargetPlatform.iOS]) {
      expect(hasPhoneCamera(isWeb: false, platform: platform), isTrue);
    }
  });

  test("a phone's browser does not", () {
    for (final platform in [TargetPlatform.android, TargetPlatform.iOS]) {
      expect(hasPhoneCamera(isWeb: true, platform: platform), isFalse);
    }
  });

  test('desktops do not', () {
    for (final platform in [
      TargetPlatform.windows,
      TargetPlatform.macOS,
      TargetPlatform.linux,
    ]) {
      expect(hasPhoneCamera(isWeb: false, platform: platform), isFalse);
    }
  });
}
