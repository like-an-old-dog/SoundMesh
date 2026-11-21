import Foundation
import Darwin
enum Pairing {
    static var defaults: UserDefaults? {
        guard let id = Bundle.main.object(forInfoDictionaryKey: "SoundMeshGroup") as? String else { return nil }
        return UserDefaults(suiteName: id)
    }
    static func pin() -> String {
        guard let defaults = defaults else { return "" }
        if let saved = defaults.string(forKey: "pin") {return saved}
        let value = String(Int.random(in: 10_000_000...99_999_999))
        defaults.set(value, forKey: "pin")
        return value
    }
    static func ipv4() -> String {
        var head: UnsafeMutablePointer<ifaddrs>?
        guard getifaddrs(&head) == 0, let first = head else {return ""}
        defer {freeifaddrs(first)}
        var cursor = head
        while let p = cursor {
            cursor = p.pointee.ifa_next
            guard String(cString: p.pointee.ifa_name) == "en0", let address = p.pointee.ifa_addr,
                  address.pointee.sa_family == UInt8(AF_INET) else {continue}
            var host = [CChar](repeating: 0, count: Int(NI_MAXHOST))
            if getnameinfo(address, socklen_t(address.pointee.sa_len), &host, socklen_t(host.count), nil, 0, NI_NUMERICHOST) == 0 {return String(cString:host)}
        }
        return ""
    }
}
