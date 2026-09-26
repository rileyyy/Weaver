import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/board/views/header/widgets/search_and_actions.dart';

void main() {
  group('searchWidthFor', () {
    test('takes a third of the available width', () {
      expect(searchWidthFor(900), 300);
    });

    test('never drops below the minimum when there is room for it', () {
      expect(searchWidthFor(300), minSearchWidth);
    });

    test('never exceeds the available width', () {
      expect(searchWidthFor(100), 100);
    });
  });

  testWidgets('the field sits right-aligned at a third of its slot', (
    tester,
  ) async {
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: Center(
            child: SizedBox(
              width: 800,
              child: SearchAndActions(
                controller: TextEditingController(),
                onSearchChanged: (_) {},
                onOpenFilters: () {},
                onLogout: () {},
              ),
            ),
          ),
        ),
      ),
    );

    final slot = tester.getRect(find.byType(LayoutBuilder));
    final field = tester.getRect(find.byType(TextField));

    expect(field.width, moreOrLessEquals(slot.width / 3));
    expect(field.right, moreOrLessEquals(slot.right));
  });
}
