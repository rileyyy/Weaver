import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/admin/widgets/admin_add_action.dart';
import 'package:weaver/features/admin/widgets/admin_list_frame.dart';

Widget _frame({AdminAddAction? addAction}) => MaterialApp(
  home: Scaffold(
    body: AdminListFrame(
      isLoading: false,
      loadError: null,
      onRetry: () async {},
      description: 'Things',
      addAction: addAction,
      child: const SizedBox(),
    ),
  ),
);

void main() {
  testWidgets('an add action shows its button, which runs it', (tester) async {
    var added = 0;
    await tester.pumpWidget(
      _frame(addAction: AdminAddAction('Add thing', onPressed: () => added++)),
    );

    await tester.tap(find.text('Add thing'));

    expect(added, 1);
  });

  testWidgets('an add action with nothing to run shows a disabled button', (
    tester,
  ) async {
    await tester.pumpWidget(
      _frame(addAction: const AdminAddAction('Add thing', onPressed: null)),
    );

    final button = tester.widget<ButtonStyleButton>(
      find.ancestor(
        of: find.text('Add thing'),
        matching: find.bySubtype<ButtonStyleButton>(),
      ),
    );
    expect(button.onPressed, isNull);
  });

  testWidgets('without an add action there is no add button', (tester) async {
    await tester.pumpWidget(_frame());

    expect(find.byIcon(Icons.add), findsNothing);
  });
}
