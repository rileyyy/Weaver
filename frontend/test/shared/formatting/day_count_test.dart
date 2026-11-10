import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/shared/formatting/day_count.dart';

void main() {
  test('one day is singular', () {
    expect(dayCount(1), '1 day');
  });

  test('any other count is plural', () {
    expect(dayCount(14), '14 days');
    expect(dayCount(3650), '3650 days');
  });
}
