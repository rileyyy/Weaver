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

  group('Admin button', () {
    Widget actions({VoidCallback? onOpenAdmin}) => MaterialApp(
      home: Scaffold(
        body: SearchAndActions(
          controller: TextEditingController(),
          onSearchChanged: (_) {},
          onOpenFilters: () {},
          onLogout: () {},
          onOpenAdmin: onOpenAdmin,
        ),
      ),
    );

    testWidgets('is hidden for users who are not admins', (tester) async {
      await tester.pumpWidget(actions());

      expect(find.byTooltip('Admin'), findsNothing);
      expect(find.byTooltip('Sign out'), findsOneWidget);
    });

    testWidgets('sits next to sign-out and opens the Admin page', (
      tester,
    ) async {
      var opened = 0;
      await tester.pumpWidget(actions(onOpenAdmin: () => opened++));

      await tester.tap(find.byTooltip('Admin'));

      expect(opened, 1);
      final gear = tester.getRect(find.byTooltip('Admin'));
      final signOut = tester.getRect(find.byTooltip('Sign out'));
      expect(gear.right, lessThanOrEqualTo(signOut.left));
    });
  });
}
