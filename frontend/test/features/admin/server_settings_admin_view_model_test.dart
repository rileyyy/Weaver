import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/admin/server_settings_admin_view_model.dart';

import 'fakes/fake_server_settings_admin_repository.dart';

void main() {
  late FakeServerSettingsAdminRepository repository;
  late ServerSettingsAdminViewModel viewModel;

  setUp(() async {
    repository = FakeServerSettingsAdminRepository(timeZoneId: 'UTC');
    viewModel = ServerSettingsAdminViewModel(repository);
    await viewModel.load();
  });

  tearDown(() => viewModel.dispose());

  test('load shows the stored time zone', () {
    expect(viewModel.timeZoneId, 'UTC');
    expect(viewModel.isLoading, isFalse);
    expect(viewModel.loadError, isNull);
  });

  test('a failed load reports an error', () async {
    repository.error = const ApiException('down', statusCode: 503);
    final vm = ServerSettingsAdminViewModel(repository);
    addTearDown(vm.dispose);

    await vm.load();

    expect(vm.loadError, isNotNull);
  });

  test('setTimeZone saves the trimmed id and shows it', () async {
    final error = await viewModel.setTimeZone('  Europe/Berlin ');

    expect(error, isNull);
    expect(repository.calls.last, 'setTimeZone Europe/Berlin');
    expect(viewModel.timeZoneId, 'Europe/Berlin');
  });

  test('setTimeZone with a blank id asks for one without a request', () async {
    final error = await viewModel.setTimeZone('   ');

    expect(error, 'Enter a time zone.');
    expect(repository.calls, ['get']);
  });

  test('setTimeZone reports the server validation message', () async {
    repository.error = const ApiException(
      "'Mars/Base' is not a known IANA time zone (400).",
      statusCode: 400,
    );

    final error = await viewModel.setTimeZone('Mars/Base');

    expect(error, "'Mars/Base' is not a known IANA time zone (400).");
    expect(viewModel.timeZoneId, 'UTC');
  });

  test('setTimeZone falls back to a generic message when offline', () async {
    repository.error = const ApiException('offline', statusCode: 503);

    final error = await viewModel.setTimeZone('Europe/Berlin');

    expect(error, 'Could not change the time zone. Try again.');
  });
}
