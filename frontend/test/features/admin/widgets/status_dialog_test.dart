import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/admin/models/status_category.dart';
import 'package:weaver/features/admin/models/status_details.dart';
import 'package:weaver/features/admin/widgets/status_dialog.dart';

import '../fakes/fake_status_admin_repository.dart';

void main() {
  late List<StatusDetails> saved;
  late String? saveError;
  bool? result;

  Future<void> open(WidgetTester tester, {bool editing = false}) async {
    saved = [];
    saveError = null;
    result = null;
    await tester.pumpWidget(
      MaterialApp(
        home: Builder(
          builder: (context) => TextButton(
            onPressed: () async {
              result = await showStatusDialog(
                context,
                initial: editing ? adminStatus('doing', name: 'Doing') : null,
                onSave: (details) async {
                  saved.add(details);
                  return saveError;
                },
              );
            },
            child: const Text('open'),
          ),
        ),
      ),
    );
    await tester.tap(find.text('open'));
    await tester.pumpAndSettle();
  }

  FilledButton submitButton(WidgetTester tester) =>
      tester.widget(find.byType(FilledButton));

  testWidgets('needs a name before it can add', (tester) async {
    await open(tester);

    expect(submitButton(tester).onPressed, isNull);

    await tester.enterText(find.widgetWithText(TextField, 'Name'), 'Review');
    await tester.pump();

    expect(submitButton(tester).onPressed, isNotNull);
  });

  testWidgets('rejects a colour that is not #RRGGBB', (tester) async {
    await open(tester);
    await tester.enterText(find.widgetWithText(TextField, 'Name'), 'Review');
    await tester.enterText(find.widgetWithText(TextField, 'Colour'), 'teal');
    await tester.pump();

    expect(find.text('Colour must look like #1E88E5.'), findsOneWidget);
    expect(submitButton(tester).onPressed, isNull);
  });

  testWidgets('a preset swatch sets the colour', (tester) async {
    await open(tester);
    await tester.enterText(find.widgetWithText(TextField, 'Name'), 'Review');
    await tester.tap(find.byTooltip('#00897B'));
    await tester.pump();
    await tester.tap(find.text('Add'));
    await tester.pumpAndSettle();

    expect(saved.single.color, '#00897B');
    expect(saved.single.category, StatusCategory.toDo);
    expect(result, isTrue);
  });

  testWidgets('stays open to show the server error', (tester) async {
    await open(tester, editing: true);
    saveError = 'A status named Done already exists (409).';
    await tester.enterText(find.widgetWithText(TextField, 'Name'), 'Done');
    await tester.tap(find.text('Save'));
    await tester.pumpAndSettle();

    expect(
      find.text('A status named Done already exists (409).'),
      findsOneWidget,
    );
    expect(find.text('Edit status'), findsOneWidget);
  });
}
