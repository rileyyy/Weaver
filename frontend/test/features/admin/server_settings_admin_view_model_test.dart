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

  test('load shows the stored settings', () {
    expect(viewModel.timeZoneId, 'UTC');
    expect(viewModel.trashRetentionDays, 30);
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

  test('setTrashRetention saves the days and shows them', () async {
    final error = await viewModel.setTrashRetention(' 90 ');

    expect(error, isNull);
    expect(repository.calls.last, 'setTrashRetention 90');
    expect(viewModel.trashRetentionDays, 90);
  });

  test('setTrashRetention accepts both ends of the range', () async {
    expect(await viewModel.setTrashRetention('1'), isNull);
    expect(await viewModel.setTrashRetention('3650'), isNull);
    expect(viewModel.trashRetentionDays, 3650);
  });

  for (final input in ['', 'abc', '0', '3651', '-5', '1.5']) {
    test('setTrashRetention refuses "$input" without a request', () async {
      final error = await viewModel.setTrashRetention(input);

      expect(error, 'Enter a whole number of days from 1 to 3650.');
      expect(repository.calls, ['get']);
    });
  }

  test('setTrashRetention reports the server validation message', () async {
    repository.error = const ApiException(
      'Only admins can change this.',
      statusCode: 403,
    );

    final error = await viewModel.setTrashRetention('90');

    expect(error, 'Only admins can change this.');
    expect(viewModel.trashRetentionDays, 30);
  });

  test(
    'setTrashRetention falls back to a generic message when offline',
    () async {
      repository.error = const ApiException('offline', statusCode: 503);

      final error = await viewModel.setTrashRetention('90');

      expect(error, 'Could not change the Trash retention. Try again.');
    },
  );
}
