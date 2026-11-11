import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/board/agenda_view_model.dart';

import 'fakes/fake_calendar_repository.dart';

void main() {
  test("load asks the server for today's date", () async {
    final viewModel = AgendaViewModel(FakeCalendarRepository());

    await viewModel.load();

    expect(viewModel.today, DateTime(2026, 11, 11));
    expect(viewModel.loadError, isNull);
  });

  test('a failed load says so, and a retry recovers', () async {
    final calendar = FakeCalendarRepository()
      ..error = const ApiException('Down', statusCode: 503);
    final viewModel = AgendaViewModel(calendar);

    await viewModel.load();

    expect(viewModel.today, isNull);
    expect(viewModel.loadError, isNotNull);

    calendar.error = null;
    await viewModel.load();

    expect(viewModel.today, DateTime(2026, 11, 11));
    expect(viewModel.loadError, isNull);
  });

  test('a reload keeps showing the last date while it waits, and a failed '
      'reload keeps it too', () async {
    final calendar = FakeCalendarRepository();
    final viewModel = AgendaViewModel(calendar);
    await viewModel.load();

    calendar.error = const ApiException('Down', statusCode: 503);
    final reload = viewModel.load();

    expect(viewModel.isLoading, isFalse);
    await reload;
    expect(viewModel.today, DateTime(2026, 11, 11));
    expect(viewModel.loadError, isNull);
  });
}
