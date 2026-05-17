# GraphNoteLM
A graph based note-taking application for students, researchers, and creatives for relational learning, study, and discovery, powered with AI insights using notebook-enclosed context.

<img width="1444" height="864" alt="image" src="https://github.com/user-attachments/assets/4bad7e72-d02d-4ec8-bc3a-c395b2fb9510" />

<img width="1916" height="937" alt="image" src="https://github.com/user-attachments/assets/f77cd55e-5e16-44c7-838a-884f81873837" />

https://github.com/user-attachments/assets/44c44208-c97f-447a-88e7-17891f9705fa

# Why GraphNoteLM?
GraphNoteLM started when I wanted a local relational note taking tool for learning AWS technologies and needing to understand which concepts I was weak on to identify bottlenecks in what I need to study before subsequent concepts.
In addition to each notebook, or "notegraph", being powered by a graph datastructure, you can apply graph algorithms to provide further insights like knowledge spans, hardest concepts to learn, etc. In addition, I liked Google's NotebookLM's utilization of turning note documents as data for LLMs to provide information specific to users whenever need answers specific to the notebook.


# Technologies
Tech Stack
- ReactJS + Vite (JavaScript, HTML & CSS)
- .NET Core 9.0 (C#)
- SignalR Websocket Connection
- PostgreSQL
- DynamoDB
- Environment-based Dockerization

Deployment
- AWS ECS Fargate
- AWS RDS
- AWS DynamoDB
- AWS S3 + CloudFront

Third Party Libraries
- Anthropic Client
- OpenAI Client

# Features
## 📓 Basic Note Taking and Saving
The application gives you basic note taking features like editting and tagging notes. Has autosave functionality so that there is no manual button you have to press to save your note.

### Importable and Exportable NoteGraphs as JSON
This gives you to option to create notegraphs without creating an account. Once you are at a good stopping point within the NoteGraph, you're able to export the notegraph as a json and reimport it again to begin writing. In addition, since all information is encapsulated within the JSON, this means that you are not limited to the NoteGraph UI. As long as your UI is able to parse the JSON file, you can create your own views.

### Cloud Storage via DynamoDB
If you want to streamline the saving process of notegraphs, you can utilize the publicly deployed GraphNoteLM. We will handle the storage of your notes securely. Furthermore, you will have access to connected LLMs through a chat, in which has access to conduct graph algorithm and gather notebook-contexted insights for you.

## 🗺️ Notebook as a Graph and Graph Algorithms
Notebooks, or "NoteGraphs" are backed up by graph datastructure, giving you not only a visual representation of your notes and connections, but also gives way to graph algorithms to be used as applications for further insights.

#### Dijkstra's Shortest Path on note nodes' user confidence rating
This can allow you to get either the hardest or easiest path of learning concepts. By conducting shortest paths on your note's confidence rates, you can find the hardest topics and concepts that you should cover. By reversing the value of the confidence score to be negative, we can find the easiest path in which you should cover.

#### Breadth First Search on minimum confidence rating
This gives the you the ability to find your "knowledge frontiers". Use this to conduct a search of all notes that you have a good grasp of, and where that grasp ends. This gives you a better idea of where and what concepts you should start refining and honing.

#### Topological sort for automatic node sequencing
Using Kahn's algorithm for topological sort, given a target node, we are able to find the best sequence of notes that you should review in that order to best understand the topic. For example, let's say you are taking an AWS Data Engineering Certification, and you need to find the best order to review and learn AWS services. Applying topological sort gives you a good idea of where to start and how to continue.

## ✨ AI Insights and Assistant
This is where the "LM" comes from I guess... Like I mentioned, I really liked Google's NotebookLM and how they used AI as an assistant for answering questions within the notebook, but I wanted to find a way to integrate LLMs with this graph-based note architecture. 

#### General Chat and Notebook-Enclosed Context
Like with NotebookLM, this is the most basic feature of the AI layer. You are able to ask the LLM questions through the built-in chat feature in regards to context specific to this notebook itself. It also has access to said graph algorithms mentioned, which gives users a more curated response and analysis of the algorithm results.
<img width="1361" height="944" alt="image" src="https://github.com/user-attachments/assets/c7c1a0fc-6276-4158-a977-7102e1148d9f" />

#### LLM Metadata
The AI has the ability to read (but not write!) to your node content. However, they do have a scratchpad for reading and writing within their own dedicated LLM Metadata section. This section can be fully customizable... you can set schemas or just have the LLM write notes to this metadata section in regards to the note content itself. Use cases for regular LLM writes would be for critcisim or review on certain note nodes, and use cases for schemas could be providing structured statistics of different data types (numericals, text, etc.)
<img width="1284" height="933" alt="image" src="https://github.com/user-attachments/assets/a2c046cc-cce0-4175-ab1d-bd6e42c4ab7e" />

#### Agentic Access to LLM Metadata, Graph Algorithms, etc.
The LLM Chatbot also has the ability to access to said services - algorithms, general read of node content, etc. - as tools. If you need a wide range of node metadata editted, this chatbot gives you the ability to do so. This essentially ties the "LM" portion with the "Graph" portion of notes, as it gives LLMs access to graph algorithm tools to make use and curate their answers for their users. This is great for non-CS or math-oriented users who have no idea how and why graphs work the way they do. The user asks questions related to graphs in natural language, and the LLM can then abstract the graph algorithm that applies to the question and give a curated answer.
<img width="1730" height="925" alt="image" src="https://github.com/user-attachments/assets/e45ccffc-f2d0-423a-9e81-591764fdd58a" />


## 💻 Options to Run/Use NoteGraphLM
### Publicly Hosted Website
We have NoteGraphLM as a publicly hosted service. However, due to hosting costs and LLM API costs, and the fact that I am broke, there is a free vs. pro version of the service. The base free version gives you all the mentioned functionalities from basic note taking (notes, tags, relationships, notes as graphs, autosaving to cloud), and you cannot use the AI Insights unless you have your own claude API key. Furthermore, you are limited up to only 5 notegraphs per user. However, only the PRO would allow you to have access to AI Insights without the need for a claude API key, and you are allowed to have unlimited notegraphs.

### Native Support to Run Entire Application Locally via Docker
If privacy is a big concern to you, a major option is running everything encased within the application within a single docker command. The docker compose will spin up everything - from Frontend, to .NET Backend Service, to even the PostgreSQL and InternalJSONStorage as volumes. All you need is to install docker, clone the repo, and run docker compose up --build. The application should be lightweight enough to be run in the background, but contains graceful shutdowns that does not disrupt data. THIS GIVES YOU ACCESS TO ALL CAPABILTIES OF NOTEGRAPH. Unlike the publicly hosted site, everything from unlimited notegraph storage to AI insights are included, granted that you have your own API key.

## Cool Applications of NoteGraphLM
### Learning Pacing Tool

<img width="1420" height="865" alt="image" src="https://github.com/user-attachments/assets/d1e84a9e-e3da-45b2-854d-fe6d774b99b5" />

This was the main motivation for me to build this tool, as a way to find the best way to learn concepts and what order to learn them. For example, you can set nodes as concepts, write note and content within them, and then connect each node to other nodes as "prerequisite to" or "has prerequisite" relationships. With the built-in confidence rate within each node's metadata, you can gage how well you have the node's concept learned and whether or not you are ready to move onto the subsequent nodes.

### Worldbuilding and Storytelling for Large Book Projects and Tabletop RPGs
You can use this NoteGraphs instead for learning, but keep an organized and visual representation of your story and in-story world as relationships and concepts. This can be especially useful for people that are into hobbies like Dungeons and Dragons, writing multi-series books that span over many in-story centuries, etc. You can have note nodes be for characters, events, items, and note relationships be connections like "allied with", "enemies with", "caused event", etc.

### Interactive Grid Game
Aside from being a primarily note-taking application, there are ways to make NoteGraphLM an interactable game with the LLM. You can define nodes as tiles or rooms, and separate unconnected nodes as "Characters" which house character specific metadata. The LLM reads your context, identifies story events and can even call BFS to get a list of all exploreable rooms for your character. After each iteration with your chatbot, they update your character's metadata on what room you are on and such.

## Planned Integrations
### WaniKani API and Progress Integration
I am a daily user of WaniKani, which is a flashcards review platform for learning Japanese Kanji and Vocabulary in a curated way. This integration aims to get a list of all the current user's learning kanji and vocab alongside the current mastery of the vocab/kanji as a numerical value. Relationship edges will be automatically linked between vocab and kanji. This is simply an API call fetch to the WaniKani API and an adapter layer that transforms the data into a NoteGraph document, which then loads to an existing or new NoteGraph.

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
### Publicly Deployed Service
You can access the service publicly through the url: xxx. You can create an account to have your notes be saved on the cloud. Otherwise, you must handle manual saving by exporting your notegraph every so often (you still have this option as a user).

### Local Development API
Clone the repository from master. Make sure you have the following installed (at the very minimum):
- .NET Core
- PostgreSQL

1. Within .NET application, provide an appsettings based off of appsettings.Example.json, the main crediential being your local postgreSQL server. ("Host=localhost;Port=5432;Database=graphnotelm;Username=postgres;Password=[yourlocalpassword]")
2. CD into the folder that contains all the code within the repo.
3. Apply migrations via Entity Framework: dotnet ef database update
4. Run the application via http or https

### Local Workspace
Clone the repository "feature/with-fe". Make sure you have docker installed.
1. CD into the folder that contains all the code within the repo.
2. run the command: docker compose --build
3. Frontend UI runs on localhost:5173

Caveats:
- You do not have access to the LLM features by default (can be turned back on, but is off due to compliance with everything being ENTIRELY local).
- Shutdown local service
   - run command: docker compose down
   - Go to docker -> Containers -> graphnotelm -> CLick the blue square button, which shuts down the container.
- Delete Internal NoteGraph Data
   - run command: docker volume rm graphnotelm_notegraph_data graphnotelm_postgres_data
   - Go to docker -> Volumes -> Check graphnotelm_notegraph_data and or graphnotelm_postgres_data -> Hit "delete" button on top right
