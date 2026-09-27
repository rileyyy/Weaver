import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/board/widgets/add_tags_dialog.dart';

/// Pumps a button that opens the dialog and records what it returns.
Future<List<List<String>?>> _pumpOpener(
  WidgetTester tester, {
  List<String> currentTags = const ['bug'],
  List<String> suggestions = const ['bug', 'design', 'backend'],
}) async {
  final results = <List<String>?>[];
  await tester.pumpWidget(
    MaterialApp(
      home: Builder(
        builder: (context) => TextButton(
          onPressed: () async => results.add(
            await showAddTagsDialog(
              context,
              currentTags: currentTags,
              suggestions: suggestions,
            ),
          ),
          child: const Text('open'),
        ),
      ),
    ),
  );
  await tester.tap(find.text('open'));
  await tester.pumpAndSettle();
  return results;
}

void main() {
  testWidgets('suggests existing tags the item does not have yet', (
    tester,
  ) async {
    await _pumpOpener(tester);

    expect(find.widgetWithText(ActionChip, 'design'), findsOneWidget);
    expect(find.widgetWithText(ActionChip, 'backend'), findsOneWidget);
    expect(find.widgetWithText(ActionChip, 'bug'), findsNothing);
  });

  testWidgets('typed and picked tags are returned together on Add', (
    tester,
  ) async {
    final results = await _pumpOpener(tester);

    await tester.enterText(find.byType(TextField), 'urgent');
    await tester.testTextInput.receiveAction(TextInputAction.done);
    await tester.pump();
    await tester.tap(find.widgetWithText(ActionChip, 'design'));
    await tester.pump();
    await tester.tap(find.widgetWithText(FilledButton, 'Add'));
    await tester.pumpAndSettle();

    expect(results.single, ['urgent', 'design']);
  });

  testWidgets('Add also includes text still in the field', (tester) async {
    final results = await _pumpOpener(tester);

    await tester.enterText(find.byType(TextField), '  release  ');
    await tester.pump();
    await tester.tap(find.widgetWithText(FilledButton, 'Add'));
    await tester.pumpAndSettle();

    expect(results.single, ['release']);
  });

  testWidgets('a tag the item already has is not added again', (tester) async {
    await _pumpOpener(tester);

    await tester.enterText(find.byType(TextField), 'BUG');
    await tester.testTextInput.receiveAction(TextInputAction.done);
    await tester.pump();

    expect(find.byType(InputChip), findsNothing);
  });

  testWidgets('a pending tag can be removed before adding', (tester) async {
    final results = await _pumpOpener(tester);

    await tester.tap(find.widgetWithText(ActionChip, 'design'));
    await tester.pump();
    await tester.tap(
      find.descendant(of: find.byType(InputChip), matching: find.byType(Icon)),
    );
    await tester.pump();

    expect(find.byType(InputChip), findsNothing);
    expect(
      tester
          .widget<FilledButton>(find.widgetWithText(FilledButton, 'Add'))
          .onPressed,
      isNull,
    );
    await tester.tap(find.text('Cancel'));
    await tester.pumpAndSettle();
    expect(results.single, isNull);
  });

  testWidgets('typing filters the suggestions', (tester) async {
    await _pumpOpener(tester);

    await tester.enterText(find.byType(TextField), 'des');
    await tester.pump();

    expect(find.widgetWithText(ActionChip, 'design'), findsOneWidget);
    expect(find.widgetWithText(ActionChip, 'backend'), findsNothing);
  });
}
