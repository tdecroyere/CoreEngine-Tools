import Cocoa

var isGameRunning = true
var isGamePaused = false

func processPendingMessages() {
    var rawEvent: NSEvent? = nil

    repeat {
        if (isGamePaused) {
            rawEvent = NSApplication.shared.nextEvent(matching: .any, until: NSDate.distantFuture, inMode: .default, dequeue: true)
        } else {
            rawEvent = NSApplication.shared.nextEvent(matching: .any, until: nil, inMode: .default, dequeue: true)
        }

        guard let event = rawEvent else {
            return
        }
        
        switch event.type {
        default:
            NSApplication.shared.sendEvent(event)
        }
    } while (rawEvent != nil && !isGamePaused)
}

autoreleasepool {
    print("CoreEditor MacOS Host")

    let delegate = MacOSAppDelegate()
    NSApplication.shared.delegate = delegate
    NSApplication.shared.activate(ignoringOtherApps: true)
    NSApplication.shared.finishLaunching()

    while (isGameRunning) {
        autoreleasepool {
            processPendingMessages()

            isGamePaused = (delegate.mainWindow.occlusionState.rawValue != 8194)

            if (!isGamePaused) {
                
            }
        }    
    }
}
