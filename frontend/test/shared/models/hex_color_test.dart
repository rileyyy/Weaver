import 'dart:ui' show Color;

import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/shared/models/hex_color.dart';

void main() {
  group('parseHexColor', () {
    test('parses #RRGGBB as an opaque colour', () {
      expect(parseHexColor('#1E88E5'), const Color(0xFF1E88E5));
    });

    test('accepts lower case and a missing #', () {
      expect(parseHexColor('1e88e5'), const Color(0xFF1E88E5));
    });

    test('returns null for malformed or missing values', () {
      for (final value in [null, '', '#12345', '#1234567', '#GGGGGG', 'red']) {
        expect(parseHexColor(value), isNull, reason: '$value');
      }
    });
  });
}
