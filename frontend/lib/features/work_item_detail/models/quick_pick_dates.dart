import 'package:weaver/core/network/api_dates.dart';
import 'package:weaver/features/work_item_detail/models/quick_pick.dart';

/// What a [QuickPick] resolves to today, as `GET /quick-picks` reports it.
class QuickPickDates {
  const QuickPickDates({
    required this.pick,
    required this.startDate,
    required this.endDate,
  });

  factory QuickPickDates.fromJson(Map<String, dynamic> json) => QuickPickDates(
    pick: QuickPick.fromWire(json['pick'] as String),
    startDate: parseCalendarDate(json['startDate'])!,
    endDate: parseCalendarDate(json['endDate'])!,
  );

  final QuickPick pick;
  final DateTime startDate;
  final DateTime endDate;
}
