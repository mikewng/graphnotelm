pages
- Login and register page
    - Login via email and password text fields (/Auth/login)
    - Link: Forgot Password?
    - Button: Register (/Auth/register)

- Home Page
    - A list view of all notes that user has (/NoteGraph/list)
    - Filterable based off of name
    - Button: Create Notegraph (/NoteGraph/create)
    - Button: Import Notegraph (/NoteGraph/import) //TBI
    - Filter: Filter by custom notegraph tags

- Auth Page
    - Change username, first & last name, password

- NoteGraph Page
    - Pulls the notegraph (/NoteGraph/id/{notegraphId:Guid})
    - Ability to edit notegraph
        - Add NoteNode
        - Edit NoteNode
        - Delete NoteNode

        - Add Tag
        - Edit Tag
        - Delete Tag

        - Add Relationship
        - Edit Relationship
        - Delete Relationship
    - Search Filter for NoteNode names
    - Filter based off of tags of notenode
    - Button: Export NoteGraphDocument as a json
    - Button: Load up NoteGraphDocument as a D3 graph of relationships and tags
        - Filter graph by tags
        - Nodes with more relationships are but higher up in depth
    - Button: Back button to return to home page