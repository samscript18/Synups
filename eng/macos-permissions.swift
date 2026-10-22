import Foundation
import ApplicationServices
let record: [String: Any] = ["verified_utc": ISO8601DateFormatter().string(from: Date()), "os": ProcessInfo.processInfo.operatingSystemVersionString, "accessibility_trusted": AXIsProcessTrusted(), "event_posting_permitted": CGPreflightPostEventAccess(), "screen_capture_permitted": CGPreflightScreenCaptureAccess(), "permission_request_made": false]
let data = try JSONSerialization.data(withJSONObject: record, options: [.prettyPrinted, .sortedKeys])
print(String(data: data, encoding: .utf8)!)
