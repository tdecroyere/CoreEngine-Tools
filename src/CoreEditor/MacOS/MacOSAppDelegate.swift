import Cocoa
import SwiftUI

class MacOSAppDelegate: NSObject, NSApplicationDelegate {
    public var mainWindow: NSWindow!

    func applicationDidFinishLaunching(_ aNotification: Notification) {
        self.buildMainMenu()

        // Create the SwiftUI view that provides the window contents.
        let contentView = ContentView()

        self.mainWindow = NSWindow(contentRect: NSMakeRect(0, 0, 1280, 720), 
                                   styleMask: [.resizable, .titled, .miniaturizable, .closable], 
                                   backing: .buffered, 
                                   defer: false)

        self.mainWindow.title = "Core Editor"
        self.mainWindow.contentView = NSHostingView(rootView: contentView)

        self.mainWindow.center()
        self.mainWindow.setFrameAutosaveName("Main Window")
        self.mainWindow.makeKeyAndOrderFront(nil)
    }
    
    func buildMainMenu() {
        let mainMenu = NSMenu(title: "MainMenu")
        
        let menuItem = mainMenu.addItem(withTitle: "ApplicationMenu", action: nil, keyEquivalent: "")
        let subMenu = NSMenu(title: "Application")
        mainMenu.setSubmenu(subMenu, for: menuItem)
        
        subMenu.addItem(withTitle: "About Core Editor", action: #selector(NSApplication.orderFrontStandardAboutPanel(_:)), keyEquivalent: "")
        subMenu.addItem(NSMenuItem.separator())
        subMenu.addItem(withTitle: "Quit", action: #selector(NSApplication.terminate(_:)), keyEquivalent: "q")
        
        NSApp.mainMenu = mainMenu
    }
}