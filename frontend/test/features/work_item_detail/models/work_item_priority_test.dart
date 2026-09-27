import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/work_item_detail/models/work_item_priority.dart';

void main() {
  test('every priority round-trips through its wire value', () {
    for (final priority in WorkItemPriority.values) {
      expect(WorkItemPriority.fromWire(priority.wire), priority);
    }
  });

  test('matches the backend enum names', () {
    expect(WorkItemPriority.values.map((p) => p.wire), [
      'Low',
      'Medium',
      'High',
      'Urgent',
    ]);
  });

  test('an unknown value is rejected rather than read as Medium', () {
    expect(() => WorkItemPriority.fromWire('Critical'), throwsFormatException);
  });
}
