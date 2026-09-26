import 'dart:ui' show Color;

import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/shared/models/work_item_status.dart';

void main() {
  group('parseStatusColor', () {
    test('parses #RRGGBB as an opaque colour', () {
      expect(parseStatusColor('#1E88E5'), const Color(0xFF1E88E5));
    });

    test('accepts lower case and a missing #', () {
      expect(parseStatusColor('1e88e5'), const Color(0xFF1E88E5));
    });

    test('returns null for malformed or missing values', () {
      for (final value in [null, '', '#12345', '#1234567', '#GGGGGG', 'red']) {
        expect(parseStatusColor(value), isNull, reason: '$value');
      }
    });
  });

  test('a status with a malformed colour still parses', () {
    final status = WorkItemStatus.fromJson({
      'id': 's1',
      'name': 'To Do',
      'order': 0,
      'color': 'not-a-colour',
    });

    expect(status.name, 'To Do');
    expect(status.color, isNull);
  });
}
