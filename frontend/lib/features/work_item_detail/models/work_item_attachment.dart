import 'package:weaver/core/network/api_dates.dart';

/// A file attached to a work item. Only the metadata; the content is
/// fetched on demand, since it can be many megabytes.
class WorkItemAttachment {
  const WorkItemAttachment({
    required this.id,
    required this.workItemId,
    required this.fileName,
    required this.contentType,
    required this.sizeBytes,
    required this.createdAt,
    this.hasThumbnail = false,
  });

  factory WorkItemAttachment.fromJson(Map<String, dynamic> json) =>
      WorkItemAttachment(
        id: json['id'] as String,
        workItemId: json['workItemId'] as String,
        fileName: json['fileName'] as String,
        contentType: json['contentType'] as String,
        sizeBytes: json['sizeBytes'] as int,
        createdAt: parseApiTimestamp(json['createdAtUtc'] as String),
        hasThumbnail: json['hasThumbnail'] as bool? ?? false,
      );

  final String id;
  final String workItemId;
  final String fileName;
  final String contentType;
  final int sizeBytes;
  final DateTime createdAt;

  /// Whether the server made a small preview of this file, which it does
  /// only for the images it can decode.
  final bool hasThumbnail;

  static const List<String> _sizeUnits = ['B', 'KB', 'MB', 'GB'];

  /// The size in the largest unit it reaches, e.g. "1.5 KB" or "25 MB";
  /// steps of 1024, as the server's size limit is set in MiB.
  String get displaySize {
    var size = sizeBytes.toDouble();
    var unit = 0;
    while (size >= 1024 && unit < _sizeUnits.length - 1) {
      size /= 1024;
      unit++;
    }
    final shown = size < 10 && unit > 0
        ? size.toStringAsFixed(1).replaceFirst(RegExp(r'\.0$'), '')
        : size.round().toString();
    return '$shown ${_sizeUnits[unit]}';
  }
}
