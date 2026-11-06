import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/admin/server_settings_admin_view_model.dart';
import 'package:weaver/features/admin/views/server_tab.dart';

import '../fakes/fake_server_settings_admin_repository.dart';

Widget _host(Widget child) => MaterialApp(home: Scaffold(body: child));

void main() {
  late FakeServerSettingsAdminRepository repository;
  late ServerSettingsAdminViewModel viewModel;

  setUp(() async {
    repository = FakeServerSettingsAdminRepository(timeZoneId: 'UTC');
    viewModel = ServerSettingsAdminViewModel(repository);
    await viewModel.load();
  });

  tearDown(() => viewModel.dispose());

  testWidgets('shows the current time zone', (tester) async {
    await tester.pumpWidget(_host(ServerTab(viewModel: viewModel)));

    expect(find.text('Time zone'), findsOneWidget);
    expect(find.text('UTC'), findsOneWidget);
  });

  testWidgets('changing the time zone saves it and shows the new one', (
    tester,
  ) async {
    await tester.pumpWidget(_host(ServerTab(viewModel: viewModel)));

    await tester.tap(find.byTooltip('Change time zone'));
    await tester.pumpAndSettle();
    await tester.enterText(find.byType(TextField), 'Europe/Berlin');
    await tester.pump();
    await tester.tap(find.text('Save'));
    await tester.pumpAndSettle();

    expect(repository.calls.last, 'setTimeZone Europe/Berlin');
    expect(find.byType(AlertDialog), findsNothing);
    expect(find.text('Europe/Berlin'), findsOneWidget);
  });

  testWidgets('an unknown time zone keeps the dialog open with the reason', (
    tester,
  ) async {
    await tester.pumpWidget(_host(ServerTab(viewModel: viewModel)));
    await tester.tap(find.byTooltip('Change time zone'));
    await tester.pumpAndSettle();
    repository.error = const ApiException(
      'Not a known IANA time zone.',
      statusCode: 400,
    );

    await tester.enterText(find.byType(TextField), 'Mars/Base');
    await tester.pump();
    await tester.tap(find.text('Save'));
    await tester.pumpAndSettle();

    expect(find.byType(AlertDialog), findsOneWidget);
    expect(find.text('Not a known IANA time zone.'), findsOneWidget);
  });

  testWidgets('shows the Trash retention and what it does', (tester) async {
    await tester.pumpWidget(_host(ServerTab(viewModel: viewModel)));

    expect(find.text('Trash retention'), findsOneWidget);
    expect(find.text('30 days'), findsOneWidget);
    expect(find.textContaining('permanently deleted after'), findsOneWidget);
  });

  testWidgets('changing the Trash retention saves it and shows the new one', (
    tester,
  ) async {
    await tester.pumpWidget(_host(ServerTab(viewModel: viewModel)));

    await tester.tap(find.byTooltip('Change Trash retention'));
    await tester.pumpAndSettle();
    await tester.enterText(find.byType(TextField), '90');
    await tester.pump();
    await tester.tap(find.text('Save'));
    await tester.pumpAndSettle();

    expect(repository.calls.last, 'setTrashRetention 90');
    expect(find.byType(AlertDialog), findsNothing);
    expect(find.text('90 days'), findsOneWidget);
  });

  testWidgets('an out-of-range retention keeps the dialog open with why', (
    tester,
  ) async {
    await tester.pumpWidget(_host(ServerTab(viewModel: viewModel)));
    await tester.tap(find.byTooltip('Change Trash retention'));
    await tester.pumpAndSettle();

    await tester.enterText(find.byType(TextField), '0');
    await tester.pump();
    await tester.tap(find.text('Save'));
    await tester.pumpAndSettle();

    expect(find.byType(AlertDialog), findsOneWidget);
    expect(
      find.text('Enter a whole number of days from 1 to 3650.'),
      findsOneWidget,
    );
    expect(repository.calls, ['get']);
  });
}
