# <img width="64" height="64" alt="favicon-g-knot-64" src="https://github.com/user-attachments/assets/444c81a5-45f2-44c0-84fb-7f359907e742" /> GraphNoteLM
A graph based note-taking application for students, researchers, and creatives for relational learning, study, and discovery, powered with AI insights using notebook-enclosed context.

<img width="1444" height="864" alt="image" src="https://github.com/user-attachments/assets/4bad7e72-d02d-4ec8-bc3a-c395b2fb9510" />

<img width="1916" height="937" alt="image" src="https://github.com/user-attachments/assets/f77cd55e-5e16-44c7-838a-884f81873837" />

https://github.com/user-attachments/assets/44c44208-c97f-447a-88e7-17891f9705fa

# Why GraphNoteLM?
GraphNoteLM started when I wanted a local relational note taking tool for learning AWS technologies and needing to understand which concepts I was weak on to identify bottlenecks in what I need to study before subsequent concepts.
In addition to each notebook, or "notegraph", being powered by a graph datastructure, you can apply graph algorithms to provide further insights like knowledge spans, hardest concepts to learn, etc. In addition, I liked Google's NotebookLM's utilization of turning note documents as data for LLMs to provide information specific to users whenever need answers specific to the notebook.


# Technologies
Tech Stack
- ElectronJS Bundling
- ReactJS + Vite (JavaScript, HTML & CSS)
- .NET 9.0 (C#)
- SignalR Websocket Connection
- SQLite (desktop app)
- PostgreSQL (accounts and graph metadata for the hosted version)
- Environment-based Dockerization

Deployment
- AWS ECS Fargate
- AWS RDS
- AWS S3 + CloudFront

Libraries
- Microsoft.Extensions.AI, with GraphNoteLM's own Anthropic and OpenAI-compatible clients (OpenAI, Ollama, or any local OpenAI-compatible endpoint)
- ModelContextProtocol for the MCP server
- Entity Framework Core, Mapperly, and Markdig
- TipTap (note editor) and D3 (graph view) in the frontend

The frontend lives in its own repository: [graphnotelm-fe](https://github.com/mikewng/graphnotelm-fe).

# Features
## 📓 Note Taking and Saving
Notes save automatically as you type, so there is no save button to press. Notes can be tagged, filed into folders, and pinned, and the Table View lets you tag or pin many notes at once. Search Note Content finds a keyword, sentence, or paragraph across every note's title and text.

### Rich Text Editing
Notes are written in a rich text editor with bold, italic, underline, strikethrough, inline code, headings, bullet and numbered lists, quotes, and code blocks. You can add images by uploading, pasting, or dragging them in, and link any word or phrase to another note.

<img width="925" height="693" alt="image" src="https://github.com/user-attachments/assets/b1a21a56-a788-46fc-ae79-14af4eb37c04" />

### Connections Between Notes
Notes connect through relationship types you define, each with a direction, a color, and an optional inverse name, so "prerequisite to" reads as "has prerequisite" from the other side. The note editor groups a note's connections by relationship type, and hovering a linked note shows a preview of it.

### Importable and Exportable NoteGraphs as JSON
Export a notegraph as JSON at any point and import it again later, whether on another install or another account. Exports include each note's flashcards and review progress, so nothing is lost along the way. In addition, since all information is encapsulated within the JSON, this means that you are not limited to the NoteGraph UI. As long as your UI is able to parse the JSON file, you can create your own views.

### Graph View
Every notegraph opens as an interactive graph. You can lay it out hierarchically, with pinned notes on top, radially around the selected note, or clustered by tag; filter it by tag; search it; focus on a note's neighborhood; and switch to a compact view for large graphs.

### Split-screen for Graph-View and NoteNode Editors
In case you need to see the graph and edit your notes simultaneously.
<img width="1907" height="988" alt="image" src="https://github.com/user-attachments/assets/b0c2291c-4c49-4364-9572-ffec7bbc7142" />

### In-Graph View Quick-edit Functionality
Edit relationships, tags, and note data within the graph view itself.

<img width="507" height="273" alt="image" src="https://github.com/user-attachments/assets/874581aa-8c66-4b92-b5ad-774693406b3e" />

### Cloud Storage (In Progress)
A hosted version with cloud storage is planned. The backend already keeps accounts and graph metadata in PostgreSQL, but storing the notes themselves in the cloud is still being built, so for now GraphNoteLM runs locally through the desktop app.

## 🗺️ Notebook as a Graph and Graph Algorithms
Notebooks, or "NoteGraphs", are backed by a graph data structure, giving you not only a visual representation of your notes and connections, but also graph algorithms that turn your notes into a study plan.

### Confidence
Every note has a confidence score from 0 to 10. Until you review a note, it's your own self-rating. Once you've reviewed it with flashcards, confidence is measured instead: it's the chance you'd still remember the note a week from now, and it fades over time until you review the note again (see [Flashcards and Spaced Repetition](#-flashcards-and-spaced-repetition)).

### Study Path
The Study Path panel runs the algorithms below without needing an AI key, and highlights the result on the graph: steps are numbered, the edges between them are drawn, and everything else is dimmed. Each algorithm can follow a single relationship type (such as "prerequisite to") in either direction, so both "A prerequisite to B" and "B depends on A" work.

#### Learning Order (Kahn's topological sort)
Pick a goal note to get everything you need to learn before it, each note coming after its prerequisites. For example, if you are taking an AWS Data Engineering Certification, this gives you an order to learn the AWS services in. Notes caught in a prerequisite cycle (found with Tarjan's algorithm) are kept together as a group and flagged, so you can spot the loop.

#### Knowledge Frontier (breadth-first search)
From a start note, walks through everything you know (confidence at or above a threshold you choose) and returns where that knowledge ends: the notes just past it, which are what to study next.

#### Weakest Path (Dijkstra's shortest path)
From a start note to a target note, finds the route through the notes you know least. That is the hardest way to the target, and it shows the concepts to shore up along it.

#### Bottlenecks
Ranks the notes holding you back the most: how many notes depend on each one, directly or through others, weighted by how far its confidence is from full. A shaky note that much of the graph builds on ranks above a weaker one that little depends on.

#### Ready to Learn
Across the whole graph, finds the notes you haven't learned yet whose prerequisites you already know. These are what you can start on right now.

## 🃏 Flashcards and Spaced Repetition
Each note can have flashcards, written in the note editor or on the Flashcards screen. That screen lists every card in the notegraph grouped by note, with search, filters for what's due, new, or missing cards, and sorting by next review.

The Review screen quizzes you on what's due: reveal the answer, then grade yourself Again, Hard, Good, or Easy (or press 1–4). Notes you forget come back later in the same session. Scheduling uses the FSRS spaced-repetition model, and every grade updates the note's measured confidence, so the Study Path and the AI assistant work from what you actually remember. Notes join the review queue once they have cards, up to 10 new notes per session, and any note can also be reviewed on demand from its own text.

## ✨ AI Insights and Assistant
This is where the "LM" comes from I guess... Like I mentioned, I really liked Google's NotebookLM and how they used AI as an assistant for answering questions within the notebook, but I wanted to find a way to integrate LLMs with this graph-based note architecture.

You can use Anthropic, OpenAI, Ollama, or any OpenAI-compatible local endpoint. Set the provider, model, and key in Settings.

#### General Chat, Notebook-Enclosed Context, and System Prompts
Like with NotebookLM, this is the most basic feature of the AI layer. You are able to ask the LLM questions through the built-in chat feature in regards to context specific to this notebook itself. It looks up note content by title, ID, or tag, and it can run all five Study Path algorithms using your measured confidence, which gives you a more curated response and analysis of the results. Each notegraph has its own editable system prompt.
<img width="1894" height="932" alt="image" src="https://github.com/user-attachments/assets/f42d3019-22b4-4690-8ccd-d7fc411d6c76" />
<img width="1361" height="944" alt="image" src="https://github.com/user-attachments/assets/c7c1a0fc-6276-4158-a977-7102e1148d9f" />
<img width="495" height="822" alt="image" src="https://github.com/user-attachments/assets/297b03dc-d947-4d51-9045-6c809e038008" />

#### LLM Metadata
The AI has the ability to read (but not write!) to your node content. However, they do have a scratchpad for reading and writing within their own dedicated LLM Metadata section. This section can be fully customizable... you can set schemas or just have the LLM write notes to this metadata section in regards to the note content itself. Use cases for regular LLM writes would be for critcisim or review on certain note nodes, and use cases for schemas could be providing structured statistics of different data types (numericals, text, etc.)
<img width="1284" height="933" alt="image" src="https://github.com/user-attachments/assets/a2c046cc-cce0-4175-ab1d-bd6e42c4ab7e" />

#### Agentic Access to Graph Algorithms
The chat assistant has the graph algorithms and note lookups as tools. This ties the "LM" portion with the "Graph" portion of notes, as it lets the LLM pick the right algorithm and curate its answer for you. This is great for non-CS or math-oriented users who have no idea how and why graphs work the way they do. The user asks questions related to graphs in natural language, like "what should I study next?", and the LLM can then abstract the graph algorithm that applies to the question and give a curated answer. The assistant reads your notes but never changes them.
<img width="1730" height="925" alt="image" src="https://github.com/user-attachments/assets/e45ccffc-f2d0-423a-9e81-591764fdd58a" />

#### AI Extraction and Creation for NoteGraphs and Notes
GraphNoteLM also have the ability to create whole notegraphs and notes from user content. Paste in any text or documents that you need, and it can be turned into a NoteGraph or NoteNode. The following image below was created entirely by pasting and processing this readme file into NoteGraphLM!
<img width="1205" height="825" alt="image" src="https://github.com/user-attachments/assets/663af55b-eeb6-4cbd-8649-282f993bf6f3" />

#### MCP Server
Connect GraphNoteLM to MCP clients such as Claude Desktop, Claude Code, ChatGPT Desktop, or Codex. They can list your notegraphs, read notes and their connections, search note content, and create new notegraphs, for example from an LLM conversation. Notes they add are marked "[DRAFT]" for you to review, and they can only edit draft notes, so an external AI can never overwrite your own writing. See [Connecting to MCP](#connecting-to-mcp).

## 💻 Options to Run/Use NoteGraphLM
### Native Support to Run Entire Application Locally via Electron
If privacy is a big concern to you, a major option is running everything encased as an executable via ElectronJS. All you have to do is download the latest executable, and the application will run. Everything is handled within the application itself, so there are no manual external management from you. This application also gives you access to all capabilites of the notegraph. Furthermore, you are able to configure your own LLM that will be run for the application. Anthropic and OpenAI clients are still open for you to use, but we support fully local environments by allowing you to run your local Ollama models and personal models.

### Support to Run Entire Application via Docker (Deprecated, use .EXE Instead!)
The docker compose will spin up everything - from Frontend, to .NET Backend Service, to even the PostgreSQL as a volume. All you need is to install docker, clone the repo, and run docker compose up --build. The application should be lightweight enough to be run in the background, but contains graceful shutdowns that does not disrupt data. THIS GIVES YOU ACCESS TO ALL CAPABILTIES OF NOTEGRAPH. Unlike the publicly hosted site, everything from unlimited notegraph storage to AI insights are included, granted that you have your own API key.

### Publicly Hosted Website
We will have NoteGraphLM as a publicly hosted service. However, due to hosting costs and LLM API costs, and the fact that I am broke, there is a free vs. pro version of the service. The base free version gives you all the mentioned functionalities from basic note taking (notes, tags, relationships, notes as graphs, autosaving to cloud), and you cannot use the AI Insights unless you have your own claude API key. Furthermore, you are limited up to only 5 notegraphs per user. However, only the PRO would allow you to have access to AI Insights without the need for a claude API key, and you are allowed to have unlimited notegraphs.

## Recent Updates
### v26.10.5
Desktop versions now follow the build date (year.month.day).

#### Study Path
- New Study Path panel with five modes: Learning Order, Knowledge Frontier, Weakest Path, Bottlenecks, and Ready to Learn. Results are highlighted right on the graph, and no AI key is needed.
- The graph algorithms were rewritten to respect each edge's direction and relationship type. Weakest Path now returns an actual path to a target, the Knowledge Frontier returns the notes just past what you know, and Learning Order is a true topological sort that reports prerequisite cycles.
- The chat assistant can run all five algorithms.

#### Flashcards and Spaced Repetition
- Flashcards on every note, a Flashcards screen listing them all, and a Review screen with FSRS scheduling.
- Confidence is now measured from your reviews and fades over time. Your self-rating is used until a note's first review.
- JSON exports include flashcards and review progress.

#### Interface
- Reworked connections in the note editor: grouped by relationship type, with previews of linked notes.
- The header now fits smaller windows by moving less-used actions into a ⋯ menu, then showing icons only.

### Patch v1.2.3
Added create notegraph tool for MCP. You can now convert your LLM conversations into notegraphs automatically!

### Patch v1.2.2
Added search throughout node content for notegraphs. This allows you to type in any keyword, sentence, or paragraph, and return a list of notes that either contain the keyword or title. Makes it easier for users to navigate through their notes!

### Patch v1.2.1
Local MCP available! You can now connect your LLM vendor with MCP capabilities for GraphNoteLM. This currently ONLY includes READ commands, as we are working on providing secure and guardrailed write tools to prevent entire note node or notegraph overwrites and other issues such as prompt injection. But for now, instead of using the AI Assistant built-in the chat, you can connect your notegraph with applications like Claude Desktop, Claude Code, ChatGPT Desktop, Codex, etc! Now, these LLMs can gain context on your graphs and answer questions for you, alongside use their own integrations to extend off GraphNoteLM!
<img width="864" height="750" alt="image" src="https://github.com/user-attachments/assets/ea9d8bf6-ef70-4e86-b6f2-4b50841badec" />

Another feature is edittable system prompts! Currently, AI Assistants are grounded with the prompt "Analyze the notegraph and its content for learning and understanding". If you want it replaced because you need a different use case (idk, like make it talk like a pirate), you can now edit the system prompt within the sidebar.

### Patch v.0.9
#### Create Graphs and Notes with AI!
GraphNoteLM can now take in user content, extract its content, creating a whole notegraph with note nodes, tags, and relationship connections for them. This might help for beginners that might not know how to GraphNoteLM or users that need a quick conversion to a GraphNote for editting later. In addition, within notegraphs, you can now create quick note nodes by clicking on the "Extract Note" button.

### Patch v.0.7
#### Official GraphNoteLM Release!
GraphNoteLM is available through your local workspace now through downloading it as an executeable or through a Docker container. I have migrated all local repository implementations to use SQLite, so that the application no longer saves it within a whole massive JSON file. 

### Patch v.0.2
#### More Efficient Writes, Saves, and Loads for NoteGraphs
Previously, the backend architecture for NoteGraphLM is that everything is ACTUALLY stored within a single JSON document locally or on DynamoDB. The goal was to move these implementations to store nodes individually from the NoteGraph, allow saves to be more efficient in writing only to a specific document instead of the entire document itself. Now, when you write to within a note, you are only writing to that note document itself, and you do not have to preprocess the entire graph each time for a save on your notes. This also does not disrupt IMPORT/EXPORT capabilities. The application still takes in the same JSON schema and outputs the same JSON schema.

## 🔜 Features Coming Soon...
### AI-Generated Flashcards and Quizzes
Have the AI draft flashcards and quizzes from your notes, so you can skip writing them and go straight to studying. Flashcards themselves are already here (see [Flashcards and Spaced Repetition](#-flashcards-and-spaced-repetition)), and reviewing them already updates your confidence.

### LLM Long Term Memory
A more long lasting memory for the AI Assistant, allowing you to be more efficient with your AI usage and makes the AI more curated and scoped to the chat. In addition, have the ability to save chats.

### Light Mode
For people that prefer a visually brighter tool. Can be toggleable and remembers your choice.

## Cool Applications of NoteGraphLM
### Learning Pacing Tool

<img width="1420" height="865" alt="image" src="https://github.com/user-attachments/assets/d1e84a9e-e3da-45b2-854d-fe6d774b99b5" />

This was the main motivation for me to build this tool, as a way to find the best way to learn concepts and what order to learn them. For example, you can set nodes as concepts, write note and content within them, and then connect each node to other nodes as "prerequisite to" or "has prerequisite" relationships. The Study Path's Learning Order then gives you the sequence to learn them in, Ready to Learn tells you what you can start on now, and Bottlenecks shows which shaky concepts are holding back the most. Reviewing your notes with flashcards keeps each note's confidence measured, so you know whether you are ready to move onto the subsequent nodes.

### Worldbuilding and Storytelling for Large Book Projects and Tabletop RPGs
You can use this NoteGraphs instead for learning, but keep an organized and visual representation of your story and in-story world as relationships and concepts. This can be especially useful for people that are into hobbies like Dungeons and Dragons, writing multi-series books that span over many in-story centuries, etc. You can have note nodes be for characters, events, items, and note relationships be connections like "allied with", "enemies with", "caused event", etc.

### Interactive Grid Game
Aside from being a primarily note-taking application, there are ways to make NoteGraphLM an interactable game with the LLM. You can define nodes as tiles or rooms, and separate unconnected nodes as "Characters" which house character specific details. The LLM reads your context, identifies story events and can even run a breadth-first search (the Knowledge Frontier) to list the rooms your character can explore. As you play, you keep track of which room your character is in within the character's note.

## My Process and What I Learned
I learned that transforming a local application in which all your logic and storage happens within your personal computer to something that is usable across many users is fundamentally different in terms of architecture. My old personal local copy was a basic datastructure in which exports a structured JSON and can be imported again to parse the JSON to be editted again. Everything happened on your computer, from writing, editting, they make live changes to the JSON document by writing directly into it from your computer. However, there eventually came a time in which I wanted access to these notes anywhere I go without the need to download and import the JSON each time I move devices, and that led me to try and build this as an API server. This brought up so many different questions even beyond architectural decisions like setting up file structure, naming, and dependency injection, for example:
- How should I store this document data, and how do users retreive that document data?
- How can users edit the document data without my API server blowing up or my database engine conducting too many read and write queries?
- How can these documents be read as graphs and conduct graph search algorihms at scale?

At this point, my first idea was to actually get the application running, working, and testable. This did not mean it had to work at scale, but for an individual user, it all functions should be correct. Thus, I chose to do a structured prototype, in which I set up the application infrastructure, and prototyped services and repositories that followed that pattern so that I could swap them later for more optimized versions. The current flow would be that:
- We retain the same JSON structure, and do so by having each NoteGraph be a document of the JSON within some DynamoDB or in-memory storage
- For allowing the ability to save and access NoteGraphs only specific to you, I decided to keep a small PostgreSQL of users, and graph metadata which has an ID that points to the NoteGraph document. I separate these databases because although we may change the document storage solution, users and their metadata rarely change, allowing us to swap document storage solutions with ease without having to write new tables or storage for users each time.
- Mapped out entry points by NoteGraph, NoteGraph Node, NoteGraph Relationships, and NoteGraph Tags.
- GraphView utility class in which turns the document into a graph, allowing for graph algorithms.

NoteGraph controller would handle anything related to creating an initial blank notegraph. This ties the notegraph with the user, creates metadata, and links that metadata with the actual notegraph document
NoteGraph Node controller would handle anything related to creating, editting, or deleting notegraph nodes. This ALSO meant editting tags and relationships within the Node.
NoteGraph Relationship and Tag controllers would handle creating or deleting the edges or tags at a NoteGraph scale. Deleting a tag within this would remove all tags that are attached to nodes within the graph, same for relationship edges.

After getting this prototype working, it was time to identify or write down what I have already identified as potential issues if this were to be scaled.

Firstly, the way I was saving and patching the node content was wrong. I should be separating the mutation of tags and relationships within the node to be its own service, as it gives the edit endpoint too much responsibility. (aka violates single responsibility principle in that we are expecting this one service method to do multiple OPTIONAL things). Furthermore, I want to give the user ability in the frontend an autosave after the user stops type for a few seconds, and sending over tags, relationships, and metadata each time when only the title and or note content is changed can cause issues.
- The solution would be to just make a separate controller for tags and relationships in which is responsible for patching tags and relationships only within the node. It essentially does the same thing, but isolates that specific functionality and allows it to be open to extra implementation.

Secondly, SQL database hosting on AWS costs surprisingly ALOT compared to DynamoDB. This might not be a problem going forward for most people, but it is a problem for me (because I am broke).
- The solution would probably be to create a separate DynamoDB from the NoteGraph Document DynamoDB, the reasoning being what I mentioned above on how I would like to swap implementations of notegraph document storage.

Thirdly, the document storage might become an issue as the NoteGraph becomes large. I did not realize until doing further research that each DynamoDB document has a 400KB limit, which means that if the JSON file exceeds it, the document would break. In addition, as the file becomes large, saving and editting within the NoteGraph becomes slower as well.
- The solution currently would be to break the document into different tables -> One for the Notegraph, its tags and relationships, and a list of IDs that reference -> NoteGraphNodes, which contain the actual data of the nodes. This would not only allow the notegraph to scale much better, but also prevents long saving times because we would be querying and editting a single node document, not the entire document itself.


## Options to Run
### Local Workspace
#### Executeable
You can find the executeables within the following dropbox link:

https://www.dropbox.com/scl/fo/mo772qt2u2onlk69j82wg/AAIELLr-dsXT1WUo1t7CKKk?rlkey=caf8qiss2samo7nx7gapxep6o&st=dvy4u0sk&dl=0

Version changes should not affect your files, as everything is stored within your %APPDATA%/graphnotelm folder.

1. All you need to do is download the latest executeable and run it.
2. Windows may give you a warning that says that this app is not authorized and is not safe, but there are no purposely malicious code inside. Download at your own peril! (I guess)

#### Building the Desktop App Yourself
Clone this repository and [graphnotelm-fe](https://github.com/mikewng/graphnotelm-fe) side by side, close the desktop app if it's running, then from the graphnotelm-fe folder:
- `node desktop.mjs` publishes this backend into the desktop app, stamps the version with today's date, builds the frontend, and starts the app.
- `node desktop.mjs --build` does the same but packages the app instead of starting it; the output goes to desktop-executable/release.

#### Connecting to MCP
For the MCP, you are able to give any LLM agentic tool access to the graphnotes by connecting it to the GraphNoteLM MCP. In the app, open Settings → Claude Desktop / MCP and press Copy: it copies a configuration with your key and the right address, ready to paste into Claude Desktop's claude_desktop_config.json. It looks like this:
```json
{
  "mcpServers": {
    "graphnotelm": {
      "command": "npx",
      "args": ["mcp-remote", "http://localhost:5000/mcp?key=YOUR_MCP_KEY"]
    }
  }
}
```


#### Docker
This is the less preferred option, as you must install Docker in order to run the application. It uses SQLite, in which saves all your data via that docker volume. HOWEVER, this also means that if you delete that docker volume, you WILL LOSE all you files within GraphNoteLM files.

0. Clone the repository "feature/with-fe". Make sure you have docker installed.
1. CD into the folder that contains all the code within the repo.
2. run the command: docker compose --build
3. Frontend UI runs on localhost:5173

Caveats:
- Shutdown local service
   - run command: docker compose down
   - Go to docker -> Containers -> graphnotelm -> CLick the blue square button, which shuts down the container.
- Delete Internal NoteGraph Data
   - run command: docker volume rm graphnotelm_notegraph_data graphnotelm_postgres_data
   - Go to docker -> Volumes -> Check graphnotelm_notegraph_data and or graphnotelm_postgres_data -> Hit "delete" button on top right


### Local Development API
Clone the repository from master. You need the .NET 9 SDK.

1. Create an appsettings.json based off of appsettings.Example.json, and add a local SQLite database under ConnectionStrings, e.g. `"LocalDB": "Data Source=%APPDATA%\\graphnotelm\\graphnotelm.db"`. The tables are created automatically on startup.
2. CD into the folder that contains all the code within the repo.
3. Run the API with `dotnet run`. It listens on http://localhost:5000, which is where the frontend's dev server (`npm run dev` in graphnotelm-fe, on localhost:5173) expects it.
4. Run the tests with `dotnet test graphnotelm.Tests`.

The PostgreSQL setup (PrimaryDB, applied with `dotnet ef database update`) is for the hosted version, which doesn't store notes yet.


### Publicly Deployed Service (TBI)
You can access the service publicly through the url: xxx. You can create an account to have your notes be saved on the cloud. Otherwise, you must handle manual saving by exporting your notegraph every so often (you still have this option as a user).
