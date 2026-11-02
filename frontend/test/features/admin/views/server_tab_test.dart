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
}
