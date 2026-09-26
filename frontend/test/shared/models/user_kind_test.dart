import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/shared/models/user_kind.dart';

void main() {
  test('parses the backend enum names', () {
    expect(UserKind.fromWire('Human'), UserKind.human);
    expect(UserKind.fromWire('Agent'), UserKind.agent);
  });

  test('an unknown value is rejected rather than read as human', () {
    expect(() => UserKind.fromWire('Robot'), throwsFormatException);
  });
}
