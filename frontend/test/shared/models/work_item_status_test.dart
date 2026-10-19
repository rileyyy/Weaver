import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/shared/models/work_item_status.dart';

void main() {
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
