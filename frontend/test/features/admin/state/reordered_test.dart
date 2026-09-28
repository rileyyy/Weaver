import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/admin/state/reordered.dart';

void main() {
  test('moves an item down', () {
    expect(reordered(['a', 'b', 'c', 'd'], 0, 2), ['b', 'c', 'a', 'd']);
  });

  test('moves an item up', () {
    expect(reordered(['a', 'b', 'c', 'd'], 3, 1), ['a', 'd', 'b', 'c']);
  });

  test('leaves the input list untouched', () {
    final items = ['a', 'b'];

    reordered(items, 0, 1);

    expect(items, ['a', 'b']);
  });
}
