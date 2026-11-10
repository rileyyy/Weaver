import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/board/views/header/space_filter_bar.dart';
import 'package:weaver/shared/models/space.dart';

const _spaces = [
  Space(id: 'home', name: 'Home'),
  Space(id: 'village', name: 'Village'),
  Space(id: 'work', name: 'Work'),
];

void main() {
  late List<Set<String>> changes;

  setUp(() => changes = []);

  Future<void> pumpBar(WidgetTester tester, Set<String> selected) =>
      tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: SpaceFilterBar(
              spaces: _spaces,
              selectedIds: selected,
              onChanged: changes.add,
            ),
          ),
        ),
      );

  bool isSelected(WidgetTester tester, String name) =>
      tester.widget<FilterChip>(find.widgetWithText(FilterChip, name)).selected;

  testWidgets('with nothing chosen, every Space shows as chosen', (
    tester,
  ) async {
    await pumpBar(tester, const {});

    expect(
      _spaces.map((s) => isSelected(tester, s.name)),
      everyElement(isTrue),
    );
  });

  testWidgets('turning one off from every Space chooses the others', (
    tester,
  ) async {
    await pumpBar(tester, const {});

    await tester.tap(find.text('Work'));

    expect(changes.single, {'home', 'village'});
  });

  testWidgets('choosing every Space again means every Space', (tester) async {
    await pumpBar(tester, const {'home', 'village'});

    await tester.tap(find.text('Work'));

    expect(changes.single, isEmpty);
  });

  testWidgets("the last chosen Space can't be turned off", (tester) async {
    await pumpBar(tester, const {'home'});

    await tester.tap(find.text('Home'));

    expect(changes, isEmpty);
  });
}
