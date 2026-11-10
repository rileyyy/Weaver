import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/shared/models/space.dart';

void main() {
  test('reads the done-hiding period', () {
    final space = Space.fromJson({
      'id': 'home',
      'name': 'Home',
      'doneHidingDays': 30,
    });

    expect(space, const Space(id: 'home', name: 'Home', doneHidingDays: 30));
  });

  test('falls back to the default period from a server that predates it', () {
    final space = Space.fromJson({'id': 'home', 'name': 'Home'});

    expect(space.doneHidingDays, Space.defaultDoneHidingDays);
    expect(space.doneHidingDays, 14);
  });

  test('Spaces that differ only in their period are not equal', () {
    expect(
      const Space(id: 'home', name: 'Home', doneHidingDays: 7),
      isNot(const Space(id: 'home', name: 'Home')),
    );
  });
}
