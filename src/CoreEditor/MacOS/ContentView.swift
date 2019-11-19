import SwiftUI

struct ContentView: View {
    var body: some View {
        List {
    Section(header: Text("UIKit"), footer: Text("We will miss you")) {
        
        Text("UITableView")
    }

    Section(header: Text("SwiftUI"), footer: Text("A lot to learn")) {
        Text("List")
    }
}.listStyle(SidebarListStyle())
        // Text("Hello, World!")
        //     .frame(maxWidth: .infinity, maxHeight: .infinity)
    }
}