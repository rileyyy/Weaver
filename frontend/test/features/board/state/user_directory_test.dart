import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/board/state/user_directory.dart';
import 'package:weaver/shared/models/user.dart';

void main() {
  final directory = UserDirectory(const [
    User(id: 'u1', username: 'alice', kind: UserKind.human),
    User(id: 'u2', username: '', kind: UserKind.agent),
  ]);

  test('resolves usernames and initials by id', () {
    expect(directory.usernameFor('u1'), 'alice');
    expect(directory.initialFor('u1'), 'A');
  });

  test('unknown, null or empty names resolve to null', () {
    expect(directory.usernameFor(null), isNull);
    expect(directory.usernameFor('nobody'), isNull);
    expect(directory.initialFor('u2'), isNull);
  });
}
